// ─────────────────────────────────────────────────────────────────────────────
// API response types — mirror the Phase 2 .NET DTOs exactly.
// ─────────────────────────────────────────────────────────────────────────────

// ── Auth ─────────────────────────────────────────────────────────────────────

export interface LoginRequest {
  email: string;
  password: string;
}

export interface TokenResponse {
  accessToken:    string;
  refreshToken:   string;
  expiresIn:      number; // seconds until access token expires
  refreshExpires: string; // ISO datetime of refresh token expiry
  user: {
    fullName:       string; // maps to AuthUser.name
    email:          string;
    allowedModules: string; // comma-separated module IDs e.g. "1,2,3"
  };
}

export interface RefreshRequest {
  refreshToken: string;
}

// ── Common ────────────────────────────────────────────────────────────────────

export interface PagedResult<T> {
  items:      T[];
  page:       number;
  pageSize:   number;
  totalCount: number;
  totalPages: number;
  hasNextPage: boolean;
  hasPrevPage: boolean;
}

// ── Stock ─────────────────────────────────────────────────────────────────────

export interface StockPieceDto {
  lengthFt: number;
  quantity: number;
  totalFeet: number; // computed: lengthFt * quantity
}

export interface StockProductDto {
  productId:    number;
  name:         string;
  category:     string;
  categoryId:   number;
  color:        string;
  gauge:        string;
  pricingMode:  'PerFoot' | 'PerUnit';
  currentStock: number;
  price:        number;
  stockPieces:  StockPieceDto[] | null; // null for PerUnit products
}

// ── Bills ─────────────────────────────────────────────────────────────────────

export type InvoiceType  = 'Standard' | 'Pvc';
export type PaymentType  = 'Cash' | 'Credit' | 'Online';
export type SaleType     = 'Full' | 'Cut';
export type GasKitType   = 'None' | 'Small' | 'Large';
export type PricingMode  = 'PerFoot' | 'PerUnit';

export interface BillListItemDto {
  invoiceId:       number;
  invoiceNumber:   string;
  invoiceType:     InvoiceType;
  customerId:      number;
  customerName:    string;
  invoiceDate:     string; // ISO date string
  subTotal:        number;
  discount:        number;
  furtherDiscount: number;
  total:           number;
  paidAmount:      number;
  paymentType:     PaymentType | null;
  balance:         number; // computed: total - paidAmount
}

export interface StandardLineItemDto {
  itemId:          number;
  productId:       number;
  productName:     string;
  pricingMode:     PricingMode;
  quantity:        number;
  sizeFt:          number | null;
  totalFeet:       number | null;
  cutFromLengthFt: number | null;
  rate:            number;
  discountPercent: number;
  discount:        number;
  lineTotal:       number;
  returnedQty:     number;
}

export interface PvcLineItemDto {
  itemId:          number;
  productId:       number;
  productName:     string;
  companyName:     string | null;
  lengthFt:        number;
  quantity:        number;
  saleType:        SaleType;
  rate:            number;
  weightPerLength: number;
  totalWeight:     number;
  totalFeet:       number;
  lengthsAmount:   number;
  gasKitType:      GasKitType;
  gasKitAmount:    number;
  discountPercent: number;
  discount:        number;
  lineTotal:       number;
  returnedQty:     number;
}

export interface ReturnSummaryDto {
  returnId:     number;
  returnNumber: string;
  returnDate:   string;
  totalAmount:  number;
  remarks:      string | null;
}

export interface BillDetailDto {
  invoiceId:       number;
  invoiceNumber:   string;
  invoiceType:     InvoiceType;
  invoiceDate:     string;
  isPosted:        boolean;
  postedDate:      string | null;
  remarks:         string | null;
  customerId:      number;
  customerName:    string;
  customerPhone:   string | null;
  customerAddress: string | null;
  subTotal:        number;
  discount:        number;
  furtherDiscount: number;
  total:           number;
  paidAmount:      number;
  paymentType:     PaymentType | null;
  totalReturned:   number;
  items:           StandardLineItemDto[]; // populated for Standard invoices
  pvcItems:        PvcLineItemDto[];      // populated for PVC invoices
  returns:         ReturnSummaryDto[];
}

// ── Customers ─────────────────────────────────────────────────────────────────

export interface CustomerSummaryDto {
  customerId:      number;
  name:            string;
  phone:           string | null;
  address:         string | null;
  openingBalance:  number;
  closingBalance:  number; // positive → owes money, zero/negative → credit/settled
}

export type LedgerEntryType = 'Bill' | 'Payment' | 'Credit' | 'Debit' | 'Return';

export interface LedgerEntryDto {
  entryId:        number;
  entryDate:      string;
  type:           LedgerEntryType;
  debit:          number;
  credit:         number;
  runningBalance: number; // cumulative balance AFTER this entry
  remarks:        string | null;
  invoiceId:      number | null; // non-null for Bill / Return → deep-link to bill detail
  invoiceNumber:  string | null;
}

export interface LedgerResponseDto {
  customerId:      number;
  customerName:    string;
  customerPhone:   string | null;
  openingBalance:  number;
  closingBalance:  number;
  entries:         LedgerEntryDto[];
  totalEntries:    number;
  page:            number;
  pageSize:        number;
  totalPages:      number;
  hasNextPage:     boolean;
  hasPrevPage:     boolean;
}

// ── Generic API error ─────────────────────────────────────────────────────────

export interface ApiError {
  message: string;
  errors?: Record<string, string[]>;
}
