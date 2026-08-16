import React, { useCallback, useEffect, useState } from 'react';
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
import { StockStackParamList } from '../../types/navigation';
import { StockProductDto } from '../../types/api';
import { stockApi } from '../../api/stockApi';
import { Colors } from '../../constants/colors';

type Props = NativeStackScreenProps<StockStackParamList, 'StockList'>;

export function StockListScreen({ navigation }: Props) {
  const [products, setProducts]   = useState<StockProductDto[]>([]);
  const [loading,  setLoading]    = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [search,   setSearch]     = useState('');
  const [error,    setError]      = useState<string | null>(null);

  const load = useCallback(async (searchTerm?: string) => {
    try {
      const data = await stockApi.getAll({ search: searchTerm });
      setProducts(data);
      setError(null);
    } catch (e: any) {
      setError(e?.response?.data?.message ?? 'Failed to load stock.');
    }
  }, []);

  useEffect(() => { load().finally(() => setLoading(false)); }, [load]);

  const onRefresh = async () => {
    setRefreshing(true);
    await load(search);
    setRefreshing(false);
  };

  const onSearch = (text: string) => {
    setSearch(text);
    load(text);
  };

  if (loading) {
    return <View style={styles.center}><ActivityIndicator size="large" color={Colors.primary} /></View>;
  }

  return (
    <View style={styles.container}>
      {/* Search bar */}
      <View style={styles.searchRow}>
        <Ionicons name="search-outline" size={18} color={Colors.textHint} style={styles.searchIcon} />
        <TextInput
          style={styles.searchInput}
          placeholder="Search products…"
          placeholderTextColor={Colors.textHint}
          value={search}
          onChangeText={onSearch}
          returnKeyType="search"
        />
        {search.length > 0 && (
          <Pressable onPress={() => onSearch('')} hitSlop={8}>
            <Ionicons name="close-circle" size={18} color={Colors.textHint} />
          </Pressable>
        )}
      </View>

      {error && (
        <View style={styles.errorBanner}>
          <Text style={styles.errorText}>{error}</Text>
          <Pressable onPress={() => load(search)}><Text style={styles.retry}>Retry</Text></Pressable>
        </View>
      )}

      <FlatList
        data={products}
        keyExtractor={item => item.productId.toString()}
        refreshControl={<RefreshControl refreshing={refreshing} onRefresh={onRefresh} />}
        contentContainerStyle={products.length === 0 ? styles.emptyContainer : styles.list}
        ListEmptyComponent={
          <View style={styles.center}>
            <Ionicons name="cube-outline" size={48} color={Colors.textHint} />
            <Text style={styles.emptyText}>No products found</Text>
          </View>
        }
        renderItem={({ item }) => (
          <Pressable
            style={styles.card}
            onPress={() => navigation.navigate('StockDetail', { productId: item.productId })}
          >
            <View style={styles.cardHeader}>
              <Text style={styles.productName} numberOfLines={1}>{item.name}</Text>
              <View style={[styles.badge,
                item.currentStock <= 0 ? styles.badgeDanger : styles.badgeSuccess]}>
                <Text style={[styles.badgeText,
                  item.currentStock <= 0 ? styles.badgeDangerText : styles.badgeSuccessText]}>
                  {item.currentStock <= 0 ? 'Out' : 'In Stock'}
                </Text>
              </View>
            </View>
            <Text style={styles.category}>{item.category} · {item.color} · {item.gauge}</Text>
            <View style={styles.cardFooter}>
              <Text style={styles.stock}>
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

const styles = StyleSheet.create({
  container:      { flex: 1, backgroundColor: Colors.background },
  center:         { flex: 1, justifyContent: 'center', alignItems: 'center' },
  emptyContainer: { flexGrow: 1, justifyContent: 'center', alignItems: 'center' },
  list:           { padding: 12 },

  searchRow: {
    flexDirection: 'row', alignItems: 'center',
    backgroundColor: Colors.surface,
    margin: 12, paddingHorizontal: 12,
    borderRadius: 10, borderWidth: 1, borderColor: Colors.border,
  },
  searchIcon:  { marginRight: 6 },
  searchInput: { flex: 1, paddingVertical: 10, fontSize: 15, color: Colors.textPrimary },

  errorBanner: {
    flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center',
    backgroundColor: Colors.dangerLight, paddingHorizontal: 16, paddingVertical: 10,
    marginHorizontal: 12, borderRadius: 8,
  },
  errorText: { color: Colors.danger, fontSize: 13, flex: 1 },
  retry:     { color: Colors.primary, fontWeight: '600', marginLeft: 8 },

  card: {
    backgroundColor: Colors.surface, borderRadius: 12,
    padding: 14, marginBottom: 10,
    shadowColor: '#000', shadowOpacity: 0.05, shadowRadius: 4,
    shadowOffset: { width: 0, height: 1 }, elevation: 2,
  },
  cardHeader:  { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', marginBottom: 4 },
  productName: { fontSize: 16, fontWeight: '600', color: Colors.textPrimary, flex: 1, marginRight: 8 },
  category:    { fontSize: 13, color: Colors.textSecondary, marginBottom: 8 },
  cardFooter:  { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' },
  stock:       { fontSize: 15, fontWeight: '600', color: Colors.textPrimary },
  price:       { fontSize: 14, color: Colors.textSecondary },

  badge:            { paddingHorizontal: 8, paddingVertical: 3, borderRadius: 6 },
  badgeSuccess:     { backgroundColor: Colors.successLight },
  badgeDanger:      { backgroundColor: Colors.dangerLight },
  badgeSuccessText: { color: Colors.success, fontSize: 12, fontWeight: '600' },
  badgeDangerText:  { color: Colors.danger,  fontSize: 12, fontWeight: '600' },
  emptyText:        { marginTop: 12, color: Colors.textSecondary, fontSize: 15 },
});
