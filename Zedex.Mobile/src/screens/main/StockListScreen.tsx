import React, { useCallback, useEffect, useMemo, useState } from 'react';
import {
  ActivityIndicator,
  FlatList,
  Pressable,
  RefreshControl,
  ScrollView,
  StyleSheet,
  Text,
  TextInput,
  View,
} from 'react-native';
import { NativeStackScreenProps } from '@react-navigation/native-stack';
import { Ionicons } from '@expo/vector-icons';
import { StockStackParamList } from '../../types/navigation';
import { StockProductDto } from '../../types/api';
import { stockApi } from '../../api/stockApi';
import { Colors } from '../../constants/colors';

type Props = NativeStackScreenProps<StockStackParamList, 'StockList'>;
type StockFilter = 'all' | 'inStock' | 'outOfStock';

// ─── Reusable chip row ────────────────────────────────────────────────────────
function ChipRow({
  label,
  options,
  value,
  onChange,
}: {
  label: string;
  options: string[];
  value: string;
  onChange: (v: string) => void;
}) {
  return (
    <View style={styles.chipSection}>
      <Text style={styles.chipLabel}>{label}</Text>
      <ScrollView horizontal showsHorizontalScrollIndicator={false}>
        {['All', ...options].map(opt => {
          const active = opt === 'All' ? value === '' : value === opt;
          return (
            <Pressable
              key={opt}
              style={[styles.chip, active && styles.chipActive]}
              onPress={() => onChange(opt === 'All' ? '' : opt)}
            >
              <Text style={[styles.chipText, active && styles.chipActiveText]}>{opt}</Text>
            </Pressable>
          );
        })}
      </ScrollView>
    </View>
  );
}

// ─── Stock availability 3-way toggle ─────────────────────────────────────────
function StockToggle({
  value,
  onChange,
}: {
  value: StockFilter;
  onChange: (v: StockFilter) => void;
}) {
  const opts: { key: StockFilter; label: string }[] = [
    { key: 'all',        label: 'All'        },
    { key: 'inStock',    label: 'In Stock'   },
    { key: 'outOfStock', label: 'Out of Stock' },
  ];
  return (
    <View style={styles.toggleRow}>
      {opts.map(o => (
        <Pressable
          key={o.key}
          style={[styles.toggleBtn, value === o.key && styles.toggleBtnActive]}
          onPress={() => onChange(o.key)}
        >
          <Text style={[styles.toggleText, value === o.key && styles.toggleTextActive]}>
            {o.label}
          </Text>
        </Pressable>
      ))}
    </View>
  );
}

// ─── Main screen ──────────────────────────────────────────────────────────────
export function StockListScreen({ navigation }: Props) {
  const [allProducts, setAllProducts] = useState<StockProductDto[]>([]);
  const [loading,     setLoading]     = useState(true);
  const [refreshing,  setRefreshing]  = useState(false);
  const [error,       setError]       = useState<string | null>(null);

  // ── Filters ──────────────────────────────────────────────────────────────
  const [search,         setSearch]         = useState('');
  const [filterCategory, setFilterCategory] = useState('');
  const [filterColor,    setFilterColor]    = useState('');
  const [filterGauge,    setFilterGauge]    = useState('');
  const [filterStock,    setFilterStock]    = useState<StockFilter>('all');
  const [showFilters,    setShowFilters]    = useState(false);

  // ── Data loading ─────────────────────────────────────────────────────────
  const load = useCallback(async () => {
    try {
      const data = await stockApi.getAll();
      setAllProducts(data);
      setError(null);
    } catch (e: any) {
      setError(e?.response?.data?.message ?? 'Failed to load stock.');
    }
  }, []);

  useEffect(() => { load().finally(() => setLoading(false)); }, [load]);

  const onRefresh = async () => {
    setRefreshing(true);
    await load();
    setRefreshing(false);
  };

  // ── Unique options from data ──────────────────────────────────────────────
  const categories = useMemo(
    () => [...new Set(allProducts.map(p => p.category).filter(Boolean))].sort(),
    [allProducts],
  );
  const colors = useMemo(
    () => [...new Set(allProducts.map(p => p.color).filter(Boolean))].sort(),
    [allProducts],
  );
  const gauges = useMemo(
    () => [...new Set(allProducts.map(p => p.gauge).filter(Boolean))].sort(),
    [allProducts],
  );

  // ── Client-side filtering ─────────────────────────────────────────────────
  const products = useMemo(() => {
    const q = search.toLowerCase();
    return allProducts.filter(p => {
      if (q && !p.name.toLowerCase().includes(q))     return false;
      if (filterCategory && p.category !== filterCategory) return false;
      if (filterColor    && p.color    !== filterColor)    return false;
      if (filterGauge    && p.gauge    !== filterGauge)    return false;
      if (filterStock === 'inStock'    && p.currentStock <= 0) return false;
      if (filterStock === 'outOfStock' && p.currentStock >  0) return false;
      return true;
    });
  }, [allProducts, search, filterCategory, filterColor, filterGauge, filterStock]);

  const activeFilterCount =
    [filterCategory, filterColor, filterGauge].filter(Boolean).length +
    (filterStock !== 'all' ? 1 : 0);

  const clearAll = () => {
    setSearch('');
    setFilterCategory('');
    setFilterColor('');
    setFilterGauge('');
    setFilterStock('all');
  };

  // ── Render ────────────────────────────────────────────────────────────────
  if (loading) {
    return (
      <View style={styles.center}>
        <ActivityIndicator size="large" color={Colors.primary} />
      </View>
    );
  }

  return (
    <View style={styles.container}>

      {/* ── Search + filter toggle ── */}
      <View style={styles.topRow}>
        <View style={styles.searchBox}>
          <Ionicons name="search-outline" size={18} color={Colors.textHint} style={styles.searchIcon} />
          <TextInput
            style={styles.searchInput}
            placeholder="Search by name…"
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

        <Pressable
          style={[styles.filterBtn, showFilters && styles.filterBtnActive]}
          onPress={() => setShowFilters(v => !v)}
        >
          <Ionicons
            name="options-outline"
            size={20}
            color={showFilters ? '#fff' : Colors.primary}
          />
          {activeFilterCount > 0 && (
            <View style={styles.filterBadge}>
              <Text style={styles.filterBadgeText}>{activeFilterCount}</Text>
            </View>
          )}
        </Pressable>
      </View>

      {/* ── Filter panel ── */}
      {showFilters && (
        <View style={styles.filterPanel}>

          {/* Stock availability */}
          <View style={styles.chipSection}>
            <Text style={styles.chipLabel}>Availability</Text>
            <StockToggle value={filterStock} onChange={setFilterStock} />
          </View>

          {categories.length > 0 && (
            <ChipRow
              label="Category"
              options={categories}
              value={filterCategory}
              onChange={setFilterCategory}
            />
          )}

          {colors.length > 0 && (
            <ChipRow
              label="Color"
              options={colors}
              value={filterColor}
              onChange={setFilterColor}
            />
          )}

          {gauges.length > 0 && (
            <ChipRow
              label="Gauge"
              options={gauges}
              value={filterGauge}
              onChange={setFilterGauge}
            />
          )}

          {activeFilterCount > 0 && (
            <Pressable style={styles.clearBtn} onPress={clearAll}>
              <Ionicons name="close-circle-outline" size={14} color={Colors.primary} />
              <Text style={styles.clearBtnText}>Clear all filters</Text>
            </Pressable>
          )}
        </View>
      )}

      {/* ── Results count ── */}
      <View style={styles.resultsRow}>
        <Text style={styles.resultsText}>
          {products.length} {products.length === 1 ? 'product' : 'products'}
          {(activeFilterCount > 0 || search) ? ' found' : ' total'}
        </Text>
      </View>

      {/* ── Error banner ── */}
      {error && (
        <View style={styles.errorBanner}>
          <Text style={styles.errorText}>{error}</Text>
          <Pressable onPress={load}>
            <Text style={styles.retry}>Retry</Text>
          </Pressable>
        </View>
      )}

      {/* ── Product list ── */}
      <FlatList
        data={products}
        keyExtractor={item => item.productId.toString()}
        refreshControl={<RefreshControl refreshing={refreshing} onRefresh={onRefresh} />}
        contentContainerStyle={
          products.length === 0 ? styles.emptyContainer : styles.list
        }
        ListEmptyComponent={
          <View style={styles.emptyInner}>
            <Ionicons name="cube-outline" size={48} color={Colors.textHint} />
            <Text style={styles.emptyText}>No products found</Text>
            {(activeFilterCount > 0 || search) && (
              <Pressable style={styles.clearBtnEmpty} onPress={clearAll}>
                <Text style={styles.clearBtnText}>Clear filters</Text>
              </Pressable>
            )}
          </View>
        }
        renderItem={({ item }) => (
          <Pressable
            style={styles.card}
            onPress={() => navigation.navigate('StockDetail', { productId: item.productId })}
          >
            <View style={styles.cardHeader}>
              <Text style={styles.productName} numberOfLines={1}>{item.name}</Text>
              <View style={[
                styles.stockBadge,
                item.currentStock <= 0 ? styles.badgeDanger : styles.badgeSuccess,
              ]}>
                <Text style={[
                  styles.stockBadgeText,
                  item.currentStock <= 0 ? styles.badgeDangerText : styles.badgeSuccessText,
                ]}>
                  {item.currentStock <= 0 ? 'Out' : 'In Stock'}
                </Text>
              </View>
            </View>

            <Text style={styles.meta}>
              {[item.category, item.color, item.gauge].filter(Boolean).join(' · ')}
            </Text>

            <View style={styles.cardFooter}>
              <Text style={styles.stockQty}>
                {item.pricingMode === 'PerFoot'
                  ? `${item.currentStock.toFixed(2)} ft`
                  : `${item.currentStock} units`}
              </Text>
              <Text style={styles.price}>Rs {item.price.toFixed(2)}</Text>
            </View>
          </Pressable>
        )}
      />
    </View>
  );
}

// ─── Styles ───────────────────────────────────────────────────────────────────
const styles = StyleSheet.create({
  container:      { flex: 1, backgroundColor: Colors.background },
  center:         { flex: 1, justifyContent: 'center', alignItems: 'center' },
  emptyContainer: { flexGrow: 1, justifyContent: 'center', alignItems: 'center' },
  list:           { padding: 12 },

  // Search + filter button
  topRow: {
    flexDirection: 'row', alignItems: 'center',
    paddingHorizontal: 12, paddingTop: 12, paddingBottom: 8, gap: 8,
  },
  searchBox: {
    flex: 1, flexDirection: 'row', alignItems: 'center',
    backgroundColor: Colors.surface, paddingHorizontal: 12,
    borderRadius: 10, borderWidth: 1, borderColor: Colors.border, height: 44,
  },
  searchIcon:  { marginRight: 6 },
  searchInput: { flex: 1, fontSize: 15, color: Colors.textPrimary },

  filterBtn: {
    width: 44, height: 44, borderRadius: 10,
    backgroundColor: Colors.surface, borderWidth: 1, borderColor: Colors.border,
    justifyContent: 'center', alignItems: 'center',
  },
  filterBtnActive: { backgroundColor: Colors.primary, borderColor: Colors.primary },
  filterBadge: {
    position: 'absolute', top: -5, right: -5,
    backgroundColor: Colors.danger, borderRadius: 8,
    minWidth: 16, height: 16,
    justifyContent: 'center', alignItems: 'center', paddingHorizontal: 3,
  },
  filterBadgeText: { color: '#fff', fontSize: 10, fontWeight: '700' },

  // Filter panel
  filterPanel: {
    backgroundColor: Colors.surface,
    marginHorizontal: 12, marginBottom: 6,
    borderRadius: 12, padding: 14,
    borderWidth: 1, borderColor: Colors.border,
  },
  chipSection: { marginBottom: 12 },
  chipLabel: {
    fontSize: 11, fontWeight: '700', color: Colors.textSecondary,
    textTransform: 'uppercase', letterSpacing: 0.6, marginBottom: 8,
  },

  // Chips
  chip: {
    paddingHorizontal: 14, paddingVertical: 7,
    borderRadius: 20, marginRight: 8,
    backgroundColor: Colors.background,
    borderWidth: 1, borderColor: Colors.border,
  },
  chipActive:     { backgroundColor: Colors.primary, borderColor: Colors.primary },
  chipText:       { fontSize: 13, color: Colors.textSecondary, fontWeight: '500' },
  chipActiveText: { color: '#fff', fontWeight: '600' },

  // Toggle
  toggleRow: { flexDirection: 'row', gap: 6 },
  toggleBtn: {
    flex: 1, paddingVertical: 8, borderRadius: 8,
    backgroundColor: Colors.background,
    borderWidth: 1, borderColor: Colors.border,
    alignItems: 'center',
  },
  toggleBtnActive:  { backgroundColor: Colors.primary, borderColor: Colors.primary },
  toggleText:       { fontSize: 12, color: Colors.textSecondary, fontWeight: '500' },
  toggleTextActive: { color: '#fff', fontWeight: '600' },

  // Clear button
  clearBtn: {
    flexDirection: 'row', alignItems: 'center', justifyContent: 'center',
    paddingTop: 4, gap: 4,
  },
  clearBtnEmpty: {
    marginTop: 14, paddingHorizontal: 20, paddingVertical: 8,
    borderRadius: 8, backgroundColor: Colors.surface,
    borderWidth: 1, borderColor: Colors.border,
  },
  clearBtnText: { color: Colors.primary, fontSize: 13, fontWeight: '600' },

  // Results count
  resultsRow:  { paddingHorizontal: 16, paddingBottom: 6 },
  resultsText: { fontSize: 12, color: Colors.textHint },

  // Error
  errorBanner: {
    flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center',
    backgroundColor: Colors.dangerLight,
    paddingHorizontal: 16, paddingVertical: 10,
    marginHorizontal: 12, borderRadius: 8, marginBottom: 8,
  },
  errorText: { color: Colors.danger, fontSize: 13, flex: 1 },
  retry:     { color: Colors.primary, fontWeight: '600', marginLeft: 8 },

  // Product card
  card: {
    backgroundColor: Colors.surface, borderRadius: 12,
    padding: 14, marginBottom: 10,
    shadowColor: '#000', shadowOpacity: 0.05, shadowRadius: 4,
    shadowOffset: { width: 0, height: 1 }, elevation: 2,
  },
  cardHeader: {
    flexDirection: 'row', alignItems: 'center',
    justifyContent: 'space-between', marginBottom: 4,
  },
  productName: {
    fontSize: 16, fontWeight: '600', color: Colors.textPrimary,
    flex: 1, marginRight: 8,
  },
  meta:      { fontSize: 13, color: Colors.textSecondary, marginBottom: 8 },
  cardFooter:{ flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' },
  stockQty:  { fontSize: 15, fontWeight: '600', color: Colors.textPrimary },
  price:     { fontSize: 14, color: Colors.textSecondary },

  stockBadge:      { paddingHorizontal: 8, paddingVertical: 3, borderRadius: 6 },
  badgeSuccess:    { backgroundColor: Colors.successLight },
  badgeDanger:     { backgroundColor: Colors.dangerLight },
  stockBadgeText:  { fontSize: 12, fontWeight: '600' },
  badgeSuccessText:{ color: Colors.success },
  badgeDangerText: { color: Colors.danger },

  emptyInner: { alignItems: 'center', padding: 24 },
  emptyText:  { marginTop: 12, color: Colors.textSecondary, fontSize: 15 },
});