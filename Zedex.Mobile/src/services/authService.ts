import * as SecureStore from 'expo-secure-store';
import * as LocalAuthentication from 'expo-local-authentication';
import { STORAGE_KEYS } from '../constants/config';
import { TokenResponse } from '../types/api';

// ─────────────────────────────────────────────────────────────────────────────
// Token persistence
// ─────────────────────────────────────────────────────────────────────────────

export async function saveTokens(tokens: TokenResponse): Promise<void> {
  await Promise.all([
    SecureStore.setItemAsync(STORAGE_KEYS.ACCESS_TOKEN,  tokens.accessToken),
    SecureStore.setItemAsync(STORAGE_KEYS.REFRESH_TOKEN, tokens.refreshToken),
  ]);
}

export async function getAccessToken(): Promise<string | null> {
  return SecureStore.getItemAsync(STORAGE_KEYS.ACCESS_TOKEN);
}

export async function getRefreshToken(): Promise<string | null> {
  return SecureStore.getItemAsync(STORAGE_KEYS.REFRESH_TOKEN);
}

export async function clearTokens(): Promise<void> {
  await Promise.all([
    SecureStore.deleteItemAsync(STORAGE_KEYS.ACCESS_TOKEN),
    SecureStore.deleteItemAsync(STORAGE_KEYS.REFRESH_TOKEN),
  ]);
}

// ─────────────────────────────────────────────────────────────────────────────
// Biometric preference
// ─────────────────────────────────────────────────────────────────────────────

export async function setBiometricEnabled(enabled: boolean): Promise<void> {
  await SecureStore.setItemAsync(
    STORAGE_KEYS.BIOMETRIC_ENABLED,
    enabled ? 'true' : 'false',
  );
}

export async function isBiometricEnabled(): Promise<boolean> {
  const val = await SecureStore.getItemAsync(STORAGE_KEYS.BIOMETRIC_ENABLED);
  return val === 'true';
}

// ─────────────────────────────────────────────────────────────────────────────
// Biometric hardware detection
// ─────────────────────────────────────────────────────────────────────────────

export interface BiometricCapability {
  isAvailable:  boolean;
  biometricType: 'fingerprint' | 'face' | 'iris' | 'none';
}

export async function checkBiometricCapability(): Promise<BiometricCapability> {
  const compatible = await LocalAuthentication.hasHardwareAsync();
  if (!compatible) return { isAvailable: false, biometricType: 'none' };

  const enrolled = await LocalAuthentication.isEnrolledAsync();
  if (!enrolled) return { isAvailable: false, biometricType: 'none' };

  const types = await LocalAuthentication.supportedAuthenticationTypesAsync();

  let biometricType: BiometricCapability['biometricType'] = 'fingerprint';
  if (types.includes(LocalAuthentication.AuthenticationType.FACIAL_RECOGNITION)) {
    biometricType = 'face';
  } else if (types.includes(LocalAuthentication.AuthenticationType.IRIS)) {
    biometricType = 'iris';
  }

  return { isAvailable: true, biometricType };
}

// ─────────────────────────────────────────────────────────────────────────────
// Biometric authentication prompt
// ─────────────────────────────────────────────────────────────────────────────

export interface BiometricAuthResult {
  success:   boolean;
  error?:    string;
  cancelled: boolean;
}

export async function promptBiometric(): Promise<BiometricAuthResult> {
  try {
    const result = await LocalAuthentication.authenticateAsync({
      promptMessage:      'Verify your identity to access Zedex',
      cancelLabel:        'Use Password',
      fallbackLabel:      'Use Password',
      disableDeviceFallback: false,
    });

    if (result.success) {
      return { success: true, cancelled: false };
    }

    const cancelled =
      result.error === 'user_cancel' ||
      result.error === 'system_cancel' ||
      result.error === 'app_cancel';

    return { success: false, cancelled, error: result.error };
  } catch (e) {
    return { success: false, cancelled: false, error: String(e) };
  }
}
