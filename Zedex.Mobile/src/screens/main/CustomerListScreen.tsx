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
import { CustomersStackParamList } from '../../types/navigation';
import { CustomerSummaryDto } from '../../types/api';
import { customersApi } from '../../api/customersApi';
import { Colors } from '../../constants/colors';

type Props = NativeStackScreenProps<CustomersStackParamList, 'CustomerList'>;

export function CustomerListScreen({ navigation }: Props) {
  const [customers,  setCustomers]  = useState<CustomerSummaryDto[]>([]);
  const [loading,    setLoading]    = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [search,     setSearch]     = useState('');
  const [error,      setError]      = useState<string | null>(null);

  const load = useCallback(async (searchTerm?: string) => {
    try {
      const data = await customersApi.getAll(searchTerm);
      setCustomers(data);
      setError(null);
    } catch (e: any) {
      setError(e?.response?.data?.message ?? 'Failed to load customers.');
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

  if (loading) return <View style={styles.center}><ActivityIndicator size="large" color={Colors.primary} /></View>;

  return (
    <View style={styles.container}>
      {/* Search */}
      <View style={styles.searchRow}>
        <Ionicons name="search-outline" size={18} color={Colors.textHint} style={styles.searchIcon} />
        <TextInput
          style={styles.searchInput}
          placeholder="Search by name or phone…"
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
        data={customers}
        keyExtractor={item => item.customerId.toString()}
        contentContainerStyle={customers.length === 0 ? styles.emptyContainer : styles.list}
        refreshControl={<RefreshControl refreshing={refreshing} onRefresh={onRefresh} />}
        ListEmptyComponent={
          <View style={styles.center}>
            <Ionicons name="people-outline" size={48} color={Colors.textHint} />
            <Text style={styles.emptyText}>No customers found</Text>
          </View>
        }
        renderItem={({ item }) => {
          const owes = item.closingBalance > 0;
          return (
            <Pressable
              style={styles.card}
              onPress={() => navigation.navigate('CustomerLedger', {
                customerId: item.customerId,
                customerName: item.name,
              })}
            >
              <View style={styles.cardTop}>
                <View style={styles.avatar}>
                  <Text style={styles.avatarText}>{item.name.charAt(0).toUpperCase()}</Text>
                </View>
                <View style={styles.info}>
                  <Text style={styles.customerName} numberOfLines={1}>{item.name}</Text>
                  {item.phone && <Text style={styles.phone}>{item.phone}</Text>}
                  {item.address && <Text style={styles.address} numberOfLines={1}>{item.address}</Text>}
                </View>
                <View style={styles.balanceBox}>
                  <Text style={[styles.balance, owes ? styles.owes : styles.credit]}>
                    Rs {Math.abs(item.closingBalance).toFixed(0)}
                  </Text>
                  <Text style={[styles.balanceLabel, owes ? styles.owes : styles.credit]}>
                    {owes ? 'Receivable' : item.closingBalance === 0 ? 'Settled' : 'Advance'}
                  </Text>
                </View>
              </View>
            </Pressable>
          );
        }}
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
  cardTop: { flexDirection: 'row', alignItems: 'center' },

  avatar: {
    width: 44, height: 44, borderRadius: 22,
    backgroundColor: Colors.primaryLight, justifyContent: 'center', alignItems: 'center',
    marginRight: 12,
  },
  avatarText: { fontSize: 18, fontWeight: '700', color: Colors.primary },

  info:         { flex: 1, marginRight: 8 },
  customerName: { fontSize: 15, fontWeight: '600', color: Colors.textPrimary },
  phone:        { fontSize: 13, color: Colors.textSecondary, marginTop: 2 },
  address:      { fontSize: 13, color: Colors.textHint, marginTop: 1 },

  balanceBox:   { alignItems: 'flex-end' },
  balance:      { fontSize: 15, fontWeight: '700' },
  balanceLabel: { fontSize: 11, marginTop: 2 },
  owes:         { color: Colors.balanceOwes },
  credit:       { color: Colors.balanceCredit },
});
