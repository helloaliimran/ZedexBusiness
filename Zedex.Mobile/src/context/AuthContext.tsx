import React, {
  createContext,
  useContext,
  useEffect,
  useReducer,
  useCallback,
} from 'react';
import { authApi } from '../api/authApi';
import { setLogoutHandler } from '../api/client';
import {
  clearTokens,
  getAccessToken,
  getRefreshToken,
  isBiometricEnabled,
  saveTokens,
} from '../services/authService';
import { LoginRequest, TokenResponse } from '../types/api';

// ─────────────────────────────────────────────────────────────────────────────
// State
// ─────────────────────────────────────────────────────────────────────────────

export interface AuthUser {
  email:   string;
  name:    string;    // sourced from API's fullName
  modules: number[];  // parsed from API's allowedModules (comma-separated string)
}

// Map the raw API user object → typed AuthUser
function mapUser(raw: TokenResponse['user']): AuthUser {
  return {
    email:   raw.email,
    name:    raw.fullName,
    modules: raw.allowedModules
      ? raw.allowedModules.split(',').filter(Boolean).map(Number)
      : [],
  };
}

type AuthState =
  | { status: 'loading' }
  | { status: 'unauthenticated' }
  | { status: 'biometric'; accessToken: string }   // tokens present, biometric lock active
  | { status: 'authenticated'; user: AuthUser; accessToken: string };

type AuthAction =
  | { type: 'RESTORE_DONE'; payload: AuthState }
  | { type: 'SIGN_IN';      payload: { user: AuthUser; accessToken: string } }
  | { type: 'BIOMETRIC_PASSED'; payload: { user: AuthUser; accessToken: string } }
  | { type: 'SIGN_OUT' };

function authReducer(state: AuthState, action: AuthAction): AuthState {
  switch (action.type) {
    case 'RESTORE_DONE':
      return action.payload;
    case 'SIGN_IN':
    case 'BIOMETRIC_PASSED':
      return { status: 'authenticated', ...action.payload };
    case 'SIGN_OUT':
      return { status: 'unauthenticated' };
    default:
      return state;
  }
}

// ─────────────────────────────────────────────────────────────────────────────
// Context
// ─────────────────────────────────────────────────────────────────────────────

interface AuthContextValue {
  state: AuthState;
  signIn:          (credentials: LoginRequest) => Promise<void>;
  biometricSignIn: (accessToken: string) => void;
  signOut:         () => Promise<void>;
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined);

// ─────────────────────────────────────────────────────────────────────────────
// Provider
// ─────────────────────────────────────────────────────────────────────────────

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [state, dispatch] = useReducer(authReducer, { status: 'loading' });

  // ── Startup: check stored tokens ──────────────────────────────────────────
  useEffect(() => {
    (async () => {
      try {
        const [accessToken, refreshToken] = await Promise.all([
          getAccessToken(),
          getRefreshToken(),
        ]);

        if (!accessToken || !refreshToken) {
          dispatch({ type: 'RESTORE_DONE', payload: { status: 'unauthenticated' } });
          return;
        }

        // Try to silently refresh to get a validated, fresh access token
        let freshTokens: TokenResponse;
        try {
          freshTokens = await authApi.refresh({ refreshToken });
          await saveTokens(freshTokens);
        } catch {
          // Refresh failed (expired / revoked) — force login
          await clearTokens();
          dispatch({ type: 'RESTORE_DONE', payload: { status: 'unauthenticated' } });
          return;
        }

        const user = mapUser(freshTokens.user);
        const biometricOn = await isBiometricEnabled();

        if (biometricOn) {
          // Keep biometric screen as the gate — lock until fingerprint passes
          dispatch({
            type: 'RESTORE_DONE',
            payload: { status: 'biometric', accessToken: freshTokens.accessToken },
          });
        } else {
          dispatch({
            type: 'RESTORE_DONE',
            payload: { status: 'authenticated', user, accessToken: freshTokens.accessToken },
          });
        }
      } catch {
        dispatch({ type: 'RESTORE_DONE', payload: { status: 'unauthenticated' } });
      }
    })();
  }, []);

  // ── Register logout handler for axios interceptor ─────────────────────────
  const handleLogout = useCallback(async () => {
    await clearTokens();
    dispatch({ type: 'SIGN_OUT' });
  }, []);

  useEffect(() => {
    setLogoutHandler(handleLogout);
  }, [handleLogout]);

  // ── Actions ───────────────────────────────────────────────────────────────

  const signIn = useCallback(async (credentials: LoginRequest) => {
    const tokens = await authApi.login(credentials);
    await saveTokens(tokens);
    dispatch({
      type: 'SIGN_IN',
      payload: { user: mapUser(tokens.user), accessToken: tokens.accessToken },
    });
  }, []);

  const biometricSignIn = useCallback((accessToken: string) => {
    // We already have user data from the startup refresh stored in the biometric state;
    // re-decode from the JWT or just dispatch with the stored token.
    // For simplicity, do a silent refresh to get the user object.
    (async () => {
      try {
        const refreshToken = await getRefreshToken();
        if (!refreshToken) throw new Error('No refresh token');
        const tokens = await authApi.refresh({ refreshToken });
        await saveTokens(tokens);
        dispatch({
          type: 'BIOMETRIC_PASSED',
          payload: { user: mapUser(tokens.user), accessToken: tokens.accessToken },
        });
      } catch {
        await clearTokens();
        dispatch({ type: 'SIGN_OUT' });
      }
    })();
  }, []);

  const signOut = useCallback(async () => {
    try {
      const [accessToken, refreshToken] = await Promise.all([
        getAccessToken(),
        getRefreshToken(),
      ]);
      if (accessToken && refreshToken) {
        await authApi.logout(accessToken, refreshToken);
      }
    } finally {
      await clearTokens();
      dispatch({ type: 'SIGN_OUT' });
    }
  }, []);

  return (
    <AuthContext.Provider value={{ state, signIn, biometricSignIn, signOut }}>
      {children}
    </AuthContext.Provider>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Hook
// ─────────────────────────────────────────────────────────────────────────────

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth must be used inside <AuthProvider>');
  return ctx;
}
