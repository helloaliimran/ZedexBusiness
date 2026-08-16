import { NavigatorScreenParams } from '@react-navigation/native';

// ── Auth stack ────────────────────────────────────────────────────────────────

export type AuthStackParamList = {
  Login:     undefined;
  Biometric: undefined;
};

// ── Main tab navigator ────────────────────────────────────────────────────────

export type MainTabParamList = {
  StockTab:     NavigatorScreenParams<StockStackParamList>;
  BillsTab:     NavigatorScreenParams<BillsStackParamList>;
  CustomersTab: NavigatorScreenParams<CustomersStackParamList>;
};

// ── Stock stack ───────────────────────────────────────────────────────────────

export type StockStackParamList = {
  StockList:   undefined;
  StockDetail: { productId: number };
};

// ── Bills stack ───────────────────────────────────────────────────────────────

export type BillsStackParamList = {
  BillsList:  undefined;
  BillDetail: { invoiceId: number };
};

// ── Customers stack ───────────────────────────────────────────────────────────

export type CustomersStackParamList = {
  CustomerList:  undefined;
  CustomerLedger: { customerId: number; customerName: string };
  BillDetail:    { invoiceId: number }; // deep-link from ledger entry
};

// ── Root navigator ────────────────────────────────────────────────────────────

export type RootStackParamList = {
  Auth: NavigatorScreenParams<AuthStackParamList>;
  Main: NavigatorScreenParams<MainTabParamList>;
};
