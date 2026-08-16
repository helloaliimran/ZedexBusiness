import React, { useCallback, useEffect, useRef, useState } from 'react';
import {
  ActivityIndicator,
  FlatList,
  Pressable,
  RefreshControl,
  StyleSheet,
  Text,
  TextInput,
  View,
} from 'react-native';
import { NativeStackScreenProps } from '@react-navigation/native-stack';
import { Ionicons } from '@expo/vector-icons';
import { BillsStackParamList } from '../../types/navigation';
import { BillListItemDto } from '../../types/api';
import { billsApi, GetBillsParams } from '../../api/billsApi';
import { Colors } from '../../constants/colors';
import { DEFAULT_PAGE_SIZE } from '../../constants/config';

type Props = NativeStackScreenProps<BillsStackParamList, 'BillsList'>;
type InvoiceTypeFilter = 'all' | 'standard' | 'pvc';

function formatDate(iso: string) {
  const d = new Date(iso);
  return d.toLocaleDateString('en-PK', { day: '2-digit', month: 'short', year: 'numeric' });
}

export function BillsListScreen({ navigation }: Props) {
  const [items,      setItems]      = useState<BillListItemDto[]>([]);
  const [page,       setPage]       = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [loading,    setLoading]    = useState(true);
  const [loadingMore, setLoadingMore] = useState(false);
  const [refreshing, setRefreshing] = useState(false);
  const [search,     setSearch]     = useState('');
  const [typeFilter, setTypeFilter] = useState<InvoiceTypeFilter>('all');
  const [error,      setError]      = useState<string | null>(null);

  const load = useCallback(async (
    p: number,
    searchTerm: string,
    type: InvoiceTypeFilter,
    append = false,
  ) => {
    const params: GetBillsParams = {
      page: p,
      pageSize: DEFAULT_PAGE_SIZE,
      search: searchTerm || undefined,
      type: type === 'all' ? undefined : type,
    };
    const result = await billsApi.getAll(params);
    setTotalPages(result.totalPages);
    setItems(prev => append ? [...prev, ...result.items] : result.items);
    setError(null);
  }, []);

  useEffect(() => {
    setPage(1);
    load(1, search, typeFilter).finally(() => setLoading(false));
  }, [search, typeFilter]); // eslint-disable-line react-hooks/exhaustive-deps

  const onRefresh = async () => {
    setRefreshing(true);
    setPage(1);
    await load(1, search, typeFilter).catch(e =>
      setError(e?.response?.data?.message ?? 'Failed to load bills.'));
    setRefreshing(false);
  };

  const onLoadMore = async () => {
    if (loadingMore || page >= totalPages) return;
    const nextPage = page + 1;
    setLoadingMore(true);
    setPage(nextPage);
    await load(nextPage, search, typeFilter, true).catch(() => {});
    setLoadingMore(false);
  };

  if (loading) {
    return <View style={styles.center}><ActivityIndicator size="large" color={Colors.primary} /></View>;
  }

  return (
    <View style={styles.container}>
      {/* Search */}
      <View style={styles.searchRow}>
        <Ionicons name="search-outline" size={18} color={Colors.textHint} style={styles.searchIcon} />
        <TextInput
          style={styles.searchInput}
          placeholder="Invoice no. or customer name…"
          placeholderTextColor={Colors.textHint}
          value={search}
          onChangeText={setSearch}
          returnKeyType="search"
        />
        {search.length > 0 && (
          <Pressable onPress={() => setSearch('')} hitSlop={8}>
            <Ionicons name="close-circle" size={18} color={Colors.textHint} />
          </Pressable>
        )}
      </View>

      {/* Type filter chips */}
      <View style={styles.chipRow}>
        {(['all', 'standard', 'pvc'] as InvoiceTypeFilter[]).map(t => (
          <Pressable
            key={t}
            style={[styles.chip, typeFilter === t && styles.chipActive]}
            onPress={() => setTypeFilter(t)}
          >
            <Text style={[styles.chipText, typeFilter === t && styles.chipTextActive]}>
              {t === 'all' ? 'All' : t === 'standard' ? 'Standard' : 'PVC'}
            </Text>
          </Pressable>
        ))}
      </View>

      {error && (
        <View style={styles.errorBanner}>
          <Text style={styles.errorText}>{error}</Text>
          <Pressable onPress={onRefresh}><Text style={styles.retry}>Retry</Text></Pressable>
        </View>
      )}

      <FlatList
        data={items}
        keyExtractor={item => item.invoiceId.toString()}
        contentContainerStyle={items.length === 0 ? styles.emptyContainer : styles.list}
        refreshControl={<RefreshControl refreshing={refreshing} onRefresh={onRefresh} />}
        onEndReached={onLoadMore}
        onEndReachedThreshold={0.3}
        ListFooterComponent={loadingMore ? <ActivityIndicator color={Colors.primary} style={{ marginVertical: 12 }} /> : null}
        ListEmptyComponent={
          <View style={styles.center}>
            <Ionicons name="document-text-outline" size={48} color={Colors.textHint} />
            <Text style={styles.emptyText}>No bills found</Text>
          </View>
        }
        renderItem={({ item }) => (
          <Pressable
            style={styles.card}
            onPress={() => navigation.navigate('BillDetail', { invoiceId: item.invoiceId })}
          >
            <View style={styles.cardTop}>
              <Text style={styles.invoiceNo}>{item.invoiceNumber}</Text>
              <View style={[styles.typeBadge,
                item.invoiceType === 'Standard' ? styles.typeBadgeStandard : styles.typeBadgePvc]}>
                <Text style={[styles.typeBadgeText,
                  item.invoiceType === 'Standard' ? styles.typeBadgeStandardText : styles.typeBadgePvcText]}>
                  {item.invoiceType}
                </Text>
              </View>
            </View>

            <Text style={styles.customerName} numberOfLines={1}>{item.customerName}</Text>
            <Text style={styles.date}>{formatDate(item.invoiceDate)}</Text>

            <View style={styles.cardFooter}>
              <View>
                <Text style={styles.footerLabel}>Total</Text>
                <Text style={styles.total}>Rs {item.total.toFixed(2)}</Text>
              </View>
              {item.balance > 0 && (
                <View style={styles.balancePill}>
                  <Text style={styles.balancePillText}>
                    Balance Rs {item.balance.toFixed(0)}
                  </Text>
                </View>
              )}
              {item.balance <= 0 && (
                <View style={[styles.balancePill, styles.paidPill]}>
                  <Text style={[styles.balancePillText, styles.paidPillText]}>Paid</Text>
                </View>
              )}
            </View>
          </Pressable>
        )}
      />
    </View>
  );
}

const styles = StyleSheet.create({
  container:      { flex: 1, backgroundColor: Colors.background },
  center:         { flex: 1, justifyContent: 'center', alignItems: 'center' },
  emptyContainer: { flexGrow: 1, justifyContent: 'center', alignItems: 'center' },
  list:           { padding: 12 },
  emptyText:      { marginTop: 12, color: Colors.textSecondary, fontSize: 15 },

  searchRow: {
    flexDirection: 'row', alignItems: 'center',
    backgroundColor: Colors.surface, margin: 12,
    paddingHorizontal: 12, borderRadius: 10,
    borderWidth: 1, borderColor: Colors.border,
  },
  searchIcon:  { marginRight: 6 },
  searchInput: { flex: 1, paddingVertical: 10, fontSize: 15, color: Colors.textPrimary },

  chipRow: { flexDirection: 'row', paddingHorizontal: 12, marginBottom: 4, gap: 8 },
  chip: {
    paddingHorizontal: 14, paddingVertical: 7, borderRadius: 20,
    borderWidth: 1, borderColor: Colors.border, backgroundColor: Colors.surface,
  },
  chipActive: { backgroundColor: Colors.primary, borderColor: Colors.primary },
  chipText:     { fontSize: 13, color: Colors.textSecondary, fontWeight: '500' },
  chipTextActive: { color: Colors.textOnPrimary },

  errorBanner: {
    flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center',
    backgroundColor: Colors.dangerLight, paddingHorizontal: 16, paddingVertical: 10,
    marginHorizontal: 12, borderRadius: 8, marginBottom: 8,
  },
  errorText: { color: Colors.danger, fontSize: 13, flex: 1 },
  retry:     { color: Colors.primary, fontWeight: '600', marginLeft: 8 },

  card: {
    backgroundColor: Colors.surface, borderRadius: 12, padding: 14, marginBottom: 10,
    shadowColor: '#000', shadowOpacity: 0.05, shadowRadius: 4,
    shadowOffset: { width: 0, height: 1 }, elevation: 2,
  },
  cardTop:      { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', marginBottom: 4 },
  invoiceNo:    { fontSize: 15, fontWeight: '700', color: Colors.textPrimary },
  customerName: { fontSize: 14, color: Colors.textSecondary, marginBottom: 2 },
  date:         { fontSize: 12, color: Colors.textHint, marginBottom: 10 },
  cardFooter:   { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'flex-end' },
  footerLabel:  { fontSize: 12, color: Colors.textSecondary },
  total:        { fontSize: 16, fontWeight: '700', color: Colors.textPrimary },

  typeBadge:            { paddingHorizontal: 8, paddingVertical: 3, borderRadius: 6 },
  typeBadgeStandard:    { backgroundColor: Colors.badgeStandard },
  typeBadgePvc:         { backgroundColor: Colors.badgePvc },
  typeBadgeText:        { fontSize: 11, fontWeight: '600' },
  typeBadgeStandardText: { color: Colors.badgeStandardText },
  typeBadgePvcText:     { color: Colors.badgePvcText },

  balancePill: {
    backgroundColor: Colors.dangerLight, paddingHorizontal: 10, paddingVertical: 4, borderRadius: 12,
  },
  balancePillText: { color: Colors.danger, fontSize: 12, fontWeight: '600' },
  paidPill:     { backgroundColor: Colors.successLight },
  paidPillText: { color: Colors.success },
});
