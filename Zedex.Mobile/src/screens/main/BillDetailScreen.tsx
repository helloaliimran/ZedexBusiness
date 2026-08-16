import React, { useEffect, useState } from 'react';
import {
  ActivityIndicator,
  ScrollView,
  StyleSheet,
  Text,
  View,
} from 'react-native';
import { NativeStackScreenProps } from '@react-navigation/native-stack';
import { BillsStackParamList } from '../../types/navigation';
import { BillDetailDto, StandardLineItemDto, PvcLineItemDto, ReturnSummaryDto } from '../../types/api';
import { billsApi } from '../../api/billsApi';
import { Colors } from '../../constants/colors';

// Screen is reused from both BillsStack and CustomersStack
type Props =
  | NativeStackScreenProps<BillsStackParamList, 'BillDetail'>
  | { route: { params: { invoiceId: number } } };

function formatDate(iso: string | null | undefined) {
  if (!iso) return '—';
  return new Date(iso).toLocaleDateString('en-PK', { day: '2-digit', month: 'short', year: 'numeric' });
}

function Row({ label, value, valueStyle }: { label: string; value: string; valueStyle?: object }) {
  return (
    <View style={styles.row}>
      <Text style={styles.rowLabel}>{label}</Text>
      <Text style={[styles.rowValue, valueStyle]}>{value}</Text>
    </View>
  );
}

export function BillDetailScreen({ route }: any) {
  const { invoiceId } = route.params as { invoiceId: number };
  const [bill,    setBill]    = useState<BillDetailDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error,   setError]   = useState<string | null>(null);

  useEffect(() => {
    billsApi.getById(invoiceId)
      .then(setBill)
      .catch(e => setError(e?.response?.data?.message ?? 'Failed to load bill.'))
      .finally(() => setLoading(false));
  }, [invoiceId]);

  if (loading) return <View style={styles.center}><ActivityIndicator size="large" color={Colors.primary} /></View>;
  if (error || !bill) return <View style={styles.center}><Text style={styles.errorText}>{error ?? 'Bill not found.'}</Text></View>;

  const balance = bill.total - bill.paidAmount;

  return (
    <ScrollView style={styles.container} contentContainerStyle={styles.content}>
      {/* Header */}
      <View style={styles.card}>
        <View style={styles.invoiceHeader}>
          <Text style={styles.invoiceNo}>{bill.invoiceNumber}</Text>
          <View style={[styles.typeBadge, bill.invoiceType === 'Standard' ? styles.typeBadgeStandard : styles.typeBadgePvc]}>
            <Text style={[styles.typeBadgeText, bill.invoiceType === 'Standard' ? styles.typeBadgeStandardText : styles.typeBadgePvcText]}>
              {bill.invoiceType}
            </Text>
          </View>
        </View>

        <Row label="Customer"  value={bill.customerName} />
        {bill.customerPhone   && <Row label="Phone"    value={bill.customerPhone} />}
        {bill.customerAddress && <Row label="Address"  value={bill.customerAddress} />}
        <Row label="Date"      value={formatDate(bill.invoiceDate)} />
        {bill.remarks         && <Row label="Remarks"  value={bill.remarks} />}
      </View>

      {/* Financial summary */}
      <View style={styles.card}>
        <Text style={styles.sectionTitle}>Summary</Text>
        <Row label="Sub Total"       value={`Rs ${bill.subTotal.toFixed(2)}`} />
        {bill.discount > 0        && <Row label="Discount"         value={`- Rs ${bill.discount.toFixed(2)}`} />}
        {bill.furtherDiscount > 0 && <Row label="Further Discount" value={`- Rs ${bill.furtherDiscount.toFixed(2)}`} />}
        {bill.totalReturned > 0   && <Row label="Returns"          value={`- Rs ${bill.totalReturned.toFixed(2)}`} />}
        <View style={styles.divider} />
        <Row label="Total"     value={`Rs ${bill.total.toFixed(2)}`} valueStyle={styles.boldValue} />
        <Row label="Paid"      value={`Rs ${bill.paidAmount.toFixed(2)}`} />
        <Row
          label="Balance"
          value={`Rs ${balance.toFixed(2)}`}
          valueStyle={balance > 0 ? styles.balanceOwes : styles.balanceClear}
        />
        {bill.paymentType && <Row label="Payment"   value={bill.paymentType} />}
      </View>

      {/* Standard line items */}
      {bill.invoiceType === 'Standard' && bill.items.length > 0 && (
        <View style={styles.card}>
          <Text style={styles.sectionTitle}>Items</Text>
          {bill.items.map((item: StandardLineItemDto) => (
            <View key={item.itemId} style={styles.lineItem}>
              <Text style={styles.lineItemName}>{item.productName}</Text>
              {item.pricingMode === 'PerFoot' ? (
                <Text style={styles.lineItemMeta}>
                  {item.quantity} pcs × {item.sizeFt} ft · Rate {item.rate}/ft
                  {item.cutFromLengthFt ? ` (cut from ${item.cutFromLengthFt} ft)` : ''}
                </Text>
              ) : (
                <Text style={styles.lineItemMeta}>
                  {item.quantity} × Rs {item.rate}
                </Text>
              )}
              {item.discount > 0 && (
                <Text style={styles.lineItemMeta}>Discount {item.discountPercent}% = Rs {item.discount.toFixed(2)}</Text>
              )}
              {item.returnedQty > 0 && (
                <Text style={[styles.lineItemMeta, { color: Colors.warning }]}>
                  Returned: {item.returnedQty}
                </Text>
              )}
              <Text style={styles.lineTotal}>Rs {item.lineTotal.toFixed(2)}</Text>
            </View>
          ))}
        </View>
      )}

      {/* PVC line items */}
      {bill.invoiceType === 'Pvc' && bill.pvcItems.length > 0 && (
        <View style={styles.card}>
          <Text style={styles.sectionTitle}>PVC Items</Text>
          {bill.pvcItems.map((item: PvcLineItemDto) => (
            <View key={item.itemId} style={styles.lineItem}>
              <Text style={styles.lineItemName}>
                {item.productName}
                {item.companyName ? ` (${item.companyName})` : ''}
              </Text>
              <Text style={styles.lineItemMeta}>
                {item.quantity} pcs × {item.lengthFt} ft · {item.saleType}
              </Text>
              <Text style={styles.lineItemMeta}>
                Weight: {item.totalWeight.toFixed(2)} kg · Rate {item.rate}/kg
              </Text>
              {item.gasKitType !== 'None' && (
                <Text style={styles.lineItemMeta}>
                  Gas kit ({item.gasKitType}): Rs {item.gasKitAmount.toFixed(2)}
                </Text>
              )}
              {item.discount > 0 && (
                <Text style={styles.lineItemMeta}>Discount {item.discountPercent}% = Rs {item.discount.toFixed(2)}</Text>
              )}
              {item.returnedQty > 0 && (
                <Text style={[styles.lineItemMeta, { color: Colors.warning }]}>
                  Returned: {item.returnedQty}
                </Text>
              )}
              <Text style={styles.lineTotal}>Rs {item.lineTotal.toFixed(2)}</Text>
            </View>
          ))}
        </View>
      )}

      {/* Returns */}
      {bill.returns.length > 0 && (
        <View style={styles.card}>
          <Text style={styles.sectionTitle}>Returns ({bill.returns.length})</Text>
          {bill.returns.map((r: ReturnSummaryDto) => (
            <View key={r.returnId} style={styles.returnRow}>
              <View>
                <Text style={styles.returnNo}>{r.returnNumber}</Text>
                <Text style={styles.returnDate}>{formatDate(r.returnDate)}</Text>
                {r.remarks && <Text style={styles.returnRemarks}>{r.remarks}</Text>}
              </View>
              <Text style={styles.returnAmt}>- Rs {r.totalAmount.toFixed(2)}</Text>
            </View>
          ))}
        </View>
      )}
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  container:  { flex: 1, backgroundColor: Colors.background },
  content:    { padding: 12 },
  center:     { flex: 1, justifyContent: 'center', alignItems: 'center', padding: 24 },
  errorText:  { color: Colors.danger, fontSize: 15, textAlign: 'center' },

  card: {
    backgroundColor: Colors.surface, borderRadius: 12, padding: 16, marginBottom: 12,
    shadowColor: '#000', shadowOpacity: 0.05, shadowRadius: 4,
    shadowOffset: { width: 0, height: 1 }, elevation: 2,
  },
  invoiceHeader: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', marginBottom: 12 },
  invoiceNo:     { fontSize: 18, fontWeight: '700', color: Colors.textPrimary },

  typeBadge:            { paddingHorizontal: 8, paddingVertical: 3, borderRadius: 6 },
  typeBadgeStandard:    { backgroundColor: Colors.badgeStandard },
  typeBadgePvc:         { backgroundColor: Colors.badgePvc },
  typeBadgeText:        { fontSize: 12, fontWeight: '600' },
  typeBadgeStandardText: { color: Colors.badgeStandardText },
  typeBadgePvcText:     { color: Colors.badgePvcText },

  row:       { flexDirection: 'row', justifyContent: 'space-between', paddingVertical: 5 },
  rowLabel:  { fontSize: 14, color: Colors.textSecondary, flex: 1 },
  rowValue:  { fontSize: 14, color: Colors.textPrimary, fontWeight: '500', textAlign: 'right', flex: 1 },
  boldValue: { fontWeight: '700', fontSize: 15 },
  balanceOwes:  { color: Colors.balanceOwes,   fontWeight: '700' },
  balanceClear: { color: Colors.balanceCredit, fontWeight: '700' },

  divider: { height: 1, backgroundColor: Colors.divider, marginVertical: 8 },

  sectionTitle: { fontSize: 16, fontWeight: '600', color: Colors.textPrimary, marginBottom: 12 },

  lineItem:     { paddingVertical: 10, borderBottomWidth: 1, borderColor: Colors.divider },
  lineItemName: { fontSize: 14, fontWeight: '600', color: Colors.textPrimary },
  lineItemMeta: { fontSize: 13, color: Colors.textSecondary, marginTop: 2 },
  lineTotal:    { fontSize: 14, fontWeight: '700', color: Colors.textPrimary, marginTop: 4 },

  returnRow:     { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'flex-start', paddingVertical: 8, borderBottomWidth: 1, borderColor: Colors.divider },
  returnNo:      { fontSize: 14, fontWeight: '600', color: Colors.textPrimary },
  returnDate:    { fontSize: 12, color: Colors.textHint },
  returnRemarks: { fontSize: 13, color: Colors.textSecondary, marginTop: 2 },
  returnAmt:     { fontSize: 14, fontWeight: '700', color: Colors.danger },
});
