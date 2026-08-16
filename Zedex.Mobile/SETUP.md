# Zedex Mobile — Setup Guide

## Prerequisites

- Node.js 18+  
- npm 9+ (or yarn)  
- Expo CLI: `npm install -g expo-cli`  
- Android Studio (for emulator) or a physical Android device with Expo Go

---

## 1. Install dependencies

```bash
cd Zedex.Mobile
npm install
```

---

## 2. Configure the API URL

Open `src/constants/config.ts`:

| Scenario | Value to set |
|---|---|
| Android emulator (default) | `10.0.2.2` + your API port — **already set** |
| Physical device (same Wi-Fi) | Replace `10.0.2.2` with your PC's LAN IP (e.g. `192.168.1.50`) |
| Production | Set the full HTTPS URL |

Your API port is whatever is in `Zedex.Api/Properties/launchSettings.json` (default: 5001 for HTTP).

---

## 3. Start the API

In Visual Studio, set **Zedex.Api** as the startup project and run it (F5 or Ctrl+F5).  
Confirm Swagger is available at `http://localhost:5001`.

---

## 4. Start the app

```bash
# Android emulator
npm run android

# Or scan QR with Expo Go on your phone
npm start
```

---

## 5. First login

Use the same email/password you use to log into the Zedex web app.  
After a successful login, you'll be offered to enable biometric authentication.

---

## Folder structure

```
Zedex.Mobile/
├── App.tsx                    ← Root component
├── app.json                   ← Expo config (bundle ID, permissions, splash)
├── package.json
├── tsconfig.json
├── babel.config.js
└── src/
    ├── constants/
    │   ├── config.ts          ← API URL, storage keys
    │   └── colors.ts          ← Design tokens
    ├── types/
    │   ├── api.ts             ← TypeScript interfaces for all API responses
    │   └── navigation.ts      ← React Navigation param lists
    ├── services/
    │   └── authService.ts     ← SecureStore + biometric helpers
    ├── api/
    │   ├── client.ts          ← Axios instance + silent refresh interceptor
    │   ├── authApi.ts
    │   ├── stockApi.ts
    │   ├── billsApi.ts
    │   └── customersApi.ts
    ├── context/
    │   └── AuthContext.tsx    ← Auth state (loading → biometric → authenticated)
    ├── navigation/
    │   ├── AppNavigator.tsx   ← Root gating (splash → auth/biometric/main)
    │   ├── AuthNavigator.tsx
    │   └── MainNavigator.tsx  ← Bottom tabs + sub-stacks
    └── screens/
        ├── auth/
        │   ├── LoginScreen.tsx
        │   └── BiometricScreen.tsx
        └── main/
            ├── StockListScreen.tsx
            ├── StockDetailScreen.tsx
            ├── BillsListScreen.tsx
            ├── BillDetailScreen.tsx
            ├── CustomerListScreen.tsx
            └── CustomerLedgerScreen.tsx
```

---

## Auth flow at startup

```
App opens
  └─ Check SecureStore for tokens
       ├─ No tokens → LoginScreen
       ├─ Tokens found → Silent refresh
       │     ├─ Refresh OK + biometric enabled → BiometricScreen (fingerprint gate)
       │     ├─ Refresh OK + no biometric     → Main app (bottom tabs)
       │     └─ Refresh failed (expired/revoked) → clear tokens → LoginScreen
       └─ (any error) → LoginScreen
```

---

## Biometric flow

- Biometric = **local device unlock only** — the server never sees a fingerprint  
- On first successful password login, the app offers to enable biometric  
- Once enabled: next app open → `BiometricScreen` auto-prompts fingerprint/Face ID  
- "Use Password Instead" → calls `signOut()` → navigates to LoginScreen

---

## Deep-link navigation

Ledger entries of type **Bill** or **Return** have a tappable row.  
Tapping navigates to `BillDetailScreen` within the Customers stack (back button returns to the ledger, not the Bills tab).

---

## Phase roadmap

| Phase | Status | Scope |
|---|---|---|
| 1 | ✅ Done | JWT API project, auth endpoints, refresh token rotation |
| 2 | ✅ Done | Stock, Bills, Customers feature APIs |
| 3 | ✅ Done | React Native scaffold, auth flow, all list/detail screens |
| 4 | 🔜 Next | Polish: pull-to-refresh, date range filters, empty states, error toasts |
| 5 | 🔜 Next | Android build (APK/AAB), app icon, splash screen |
