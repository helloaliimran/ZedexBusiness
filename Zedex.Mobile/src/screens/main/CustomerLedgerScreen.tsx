import React, { useCallback, useEffect, useState } from 'react';
import {
  ActivityIndicator,
  FlatList,
  Pressable,
  RefreshControl,
  StyleSheet,
  Text,
  View,
} from 'react-native';
import { NativeStackScreenProps } from '@react-navigation/native-stack';
import { Ionicons } from '@expo/vector-icons';
import { CustomersStackParamList } from '../../types/navigation';
import { LedgerEntryDto, LedgerResponseDto } from '../../types/api';
import { customersApi } from '../../api/customersApi';
import { Colors } from '../../constants/colors';

type Props = NativeStackScreenProps<CustomersStackParamList, 'CustomerLedger'>;

function formatDate(iso: string) {
  return new Date(iso).toLocaleDateString('en-PK', { day: '2-digit', month: 'short', year: 'numeric' });
}

const ENTRY_TYPE_ICON: Record<string, { name: keyof typeof Ionicons.glyphMap; color: string }> = {
  Bill:    { name: 'document-text-outline', color: Colors.primary },
  Return:  { name: 'return-down-back-outline', color: Colors.warning },
  Payment: { name: 'cash-outline',            color: Colors.success },
  Credit:  { name: 'add-circle-outline',      color: Colors.success },
  Debit:   { name: 'remove-circle-outline',   color: Colors.danger },
};

export function CustomerLedgerScreen({ route, navigation }: Props) {
  const { customerId } = route.params;

  const [ledger,    setLedger]    = useState<LedgerResponseDto | null>(null);
  const [page,      setPage]      = useState(1);
  const [loading,   setLoading]   = useState(true);
  const [loadingMore, setLoadingMore] = useState(false);
  const [refreshing,  setRefreshing]  = useState(false);
  const [error,     setError]     = useState<string | null>(null);

  const loadPage = useCallback(async (p: number, append = false) => {
    const data = await customersApi.getLedger(customerId, { page: p });
    setLedger(prev =>
      append && prev
        ? { ...data, entries: [...prev.entries, ...data.entries] }
        : data,
    );
    setError(null);
  }, [customerId]);

  useEffect(() => {
    loadPage(1).finally(() => setLoading(false));
  }, [loadPage]);

  const onRefresh = async () => {
    setRefreshing(true);
    setPage(1);
    await loadPage(1).catch(e => setError(e?.response?.data?.message ?? 'Failed to load ledger.'));
    setRefreshing(false);
  };

  const onLoadMore = async () => {
    if (!ledger || loadingMore || page >= ledger.totalPages) return;
    const nextPage = page + 1;
    setLoadingMore(true);
    setPage(nextPage);
    await loadPage(nextPage, true).catch(() => {});
    setLoadingMore(false);
  };

  if (loading) return <View style={styles.center}><ActivityIndicator size="large" color={Colors.primary} /></View>;
  if (error || !ledger) return <View style={styles.center}><Text style={styles.errorText}>{error ?? 'Failed to load.'}</Text></View>;

  const owes = ledger.closingBalance > 0;

  return (
    <FlatList
      style={styles.container}
      data={ledger.entries}
      keyExtractor={item => item.entryId.toString()}
      contentContainerStyle={ledger.entries.length === 0 ? styles.emptyContainer : styles.list}
      refreshControl={<RefreshControl refreshing={refreshing} onRefresh={onRefresh} />}
      onEndReached={onLoadMore}
      onEndReachedThreshold={0.3}
      ListHeaderComponent={
        <>
          {/* Balance header card */}
          <View style={[styles.balanceCard, owes ? styles.balanceCardOwes : styles.balanceCardCredit]}>
            <Text style={styles.balanceName}>{ledger.customerName}</Text>
            {ledger.customerPhone && <Text style={styles.balancePhone}>{ledger.customerPhone}</Text>}
            <Text style={styles.balanceAmtLabel}>{owes ? 'Receivable' : ledger.closingBalance === 0 ? 'Settled' : 'Advance'}</Text>
            <Text style={styles.balanceAmt}>Rs {Math.abs(ledger.closingBalance).toFixed(2)}</Text>
            <Text style={styles.openingInfo}>Opening: Rs {ledger.openingBalance.toFixed(2)}</Text>
          </View>

          {error && (
            <View style={styles.errorBanner}>
              <Text style={styles.errorText}>{error}</Text>
            </View>
          )}
        </>
      }
      ListFooterComponent={loadingMore ? <ActivityIndicator color={Colors.primary} style={{ marginVertical: 12 }} /> : null}
      ListEmptyComponent={
        <View style={styles.centerInner}>
          <Ionicons name="receipt-outline" size={48} color={Colors.textHint} />
          <Text style={styles.emptyText}>No ledger entries</Text>
        </View>
      }
      renderItem={({ item }: { item: LedgerEntryDto }) => {
        const icon = ENTRY_TYPE_ICON[item.type] ?? ENTRY_TYPE_ICON.Bill;
        const balancePositive = item.runningBalance > 0;
        const isTappable = item.invoiceId != null;

        return (
          <Pressable
            style={styles.entryCard}
            onPress={isTappable ? () => navigation.navigate('BillDetail', { invoiceId: item.invoiceId! }) : undefined}
            disabled={!isTappable}
          >
            {/* Icon */}
            <View style={[styles.iconBox, { backgroundColor: icon.color + '1A' }]}>
              <Ionicons name={icon.name} size={20} color={icon.color} />
            </View>

            {/* Middle: type, date, remarks, invoice# */}
            <View style={styles.entryMid}>
              <Text style={styles.entryType}>{item.type}</Text>
              <Text style={styles.entryDate}>{formatDate(item.entryDate)}</Text>
              {item.invoiceNumber && (
                <Text style={styles.entryInvoice}>{item.invoiceNumber}</Text>
              )}
              {item.remarks && (
                <Text style={styles.entryRemarks} numberOfLines={1}>{item.remarks}</Text>
              )}
            </View>

            {/* Right: debit/credit + running balance */}
            <View style={styles.entryRight}>
              {item.debit > 0 && (
                <Text style={styles.entryDebit}>+ Rs {item.debit.toFixed(0)}</Text>
              )}
              {item.credit > 0 && (
                <Text style={styles.entryCredit}>- Rs {item.credit.toFixed(0)}</Text>
              )}
              <Text style={[styles.runningBalance, balancePositive ? styles.owes : styles.credit]}>
                Rs {Math.abs(item.runningBalance).toFixed(0)}
              </Text>
            </View>

            {isTappable && (
              <Ionicons name="chevron-forward" size={14} color={Colors.textHint} style={styles.chevron} />
            )}
          </Pressable>
        );
      }}
    />
  );
}

const styles = StyleSheet.create({
  container:      { flex: 1, backgroundColor: Colors.background },
  list:           { padding: 12 },
  emptyContainer: { flexGrow: 1 },
  center:         { flex: 1, justifyContent: 'center', alignItems: 'center' },
  centerInner:    { justifyContent: 'center', alignItems: 'center', padding: 40 },
  emptyText:      { marginTop: 12, color: Colors.textSecondary, fontSize: 15 },

  balanceCard: { margin: 12, borderRadius: 16, padding: 20, alignItems: 'center' },
  balanceCardOwes:   { backgroundColor: Colors.dangerLight },
  balanceCardCredit: { backgroundColor: Colors.successLight },
  balanceName:    { fontSize: 18, fontWeight: '700', color: Colors.textPrimary },
  balancePhone:   { fontSize: 14, color: Colors.textSecondary, marginTop: 2 },
  balanceAmtLabel:{ fontSize: 13, color: Colors.textSecondary, marginTop: 12 },
  balanceAmt:     { fontSize: 28, fontWeight: '800', color: Colors.textPrimary, marginTop: 4 },
  openingInfo:    { fontSize: 12, color: Colors.textHint, marginTop: 8 },

  errorBanner: {
    backgroundColor: Colors.dangerLight, marginHorizontal: 12, borderRadius: 8,
    paddingHorizontal: 16, paddingVertical: 10, marginBottom: 8,
  },
  errorText: { color: Colors.danger, fontSize: 13 },

  entryCard: {
    backgroundColor: Colors.surface, borderRadius: 12, padding: 12, marginBottom: 8,
    flexDirection: 'row', alignItems: 'center',
    shadowColor: '#000', shadowOpacity: 0.05, shadowRadius: 4,
    shadowOffset: { width: 0, height: 1 }, elevation: 2,
  },
  iconBox: { width: 40, height: 40, borderRadius: 20, justifyContent: 'center', alignItems: 'center', marginRight: 10 },

  entryMid:     { flex: 1 },
  entryType:    { fontSize: 14, fontWeight: '600', color: Colors.textPrimary },
  entryDate:    { fontSize: 12, color: Colors.textHint, marginTop: 1 },
  entryInvoice: { fontSize: 12, color: Colors.primary, marginTop: 1 },
  entryRemarks: { fontSize: 12, color: Colors.textSecondary, marginTop: 1 },

  entryRight:      { alignItems: 'flex-end', marginLeft: 8 },
  entryDebit:      { fontSize: 13, color: Colors.danger, fontWeight: '600' },
  entryCredit:     { fontSize: 13, color: Colors.success, fontWeight: '600' },
  runningBalance:  { fontSize: 13, fontWeight: '700', marginTop: 4 },
  owes:            { color: Colors.balanceOwes },
  credit:          { color: Colors.balanceCredit },

  chevron: { marginLeft: 4 },
});
