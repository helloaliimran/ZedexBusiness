import React, { useEffect, useState } from 'react';
import {
  ActivityIndicator,
  FlatList,
  ScrollView,
  StyleSheet,
  Text,
  View,
} from 'react-native';
import { NativeStackScreenProps } from '@react-navigation/native-stack';
import { StockStackParamList } from '../../types/navigation';
import { StockProductDto, StockPieceDto } from '../../types/api';
import { stockApi } from '../../api/stockApi';
import { Colors } from '../../constants/colors';

type Props = NativeStackScreenProps<StockStackParamList, 'StockDetail'>;

export function StockDetailScreen({ route }: Props) {
  const { productId } = route.params;
  const [product, setProduct] = useState<StockProductDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error,   setError]   = useState<string | null>(null);

  useEffect(() => {
    stockApi.getById(productId)
      .then(setProduct)
      .catch(e => setError(e?.response?.data?.message ?? 'Failed to load product.'))
      .finally(() => setLoading(false));
  }, [productId]);

  if (loading) {
    return <View style={styles.center}><ActivityIndicator size="large" color={Colors.primary} /></View>;
  }
  if (error || !product) {
    return <View style={styles.center}><Text style={styles.errorText}>{error ?? 'Product not found.'}</Text></View>;
  }

  return (
    <ScrollView style={styles.container} contentContainerStyle={styles.content}>
      {/* Header */}
      <View style={styles.card}>
        <Text style={styles.productName}>{product.name}</Text>
        <Text style={styles.meta}>{product.category} · {product.color} · {product.gauge}</Text>
      </View>

      {/* Key stats */}
      <View style={styles.statsRow}>
        <View style={styles.statBox}>
          <Text style={styles.statLabel}>Current Stock</Text>
          <Text style={styles.statValue}>
            {product.pricingMode === 'PerFoot'
              ? `${product.currentStock.toFixed(2)} ft`
              : `${product.currentStock} units`}
          </Text>
        </View>
        <View style={styles.statBox}>
          <Text style={styles.statLabel}>Price</Text>
          <Text style={styles.statValue}>Rs {product.price.toFixed(2)}</Text>
        </View>
        <View style={styles.statBox}>
          <Text style={styles.statLabel}>Pricing</Text>
          <Text style={styles.statValue}>{product.pricingMode}</Text>
        </View>
      </View>

      {/* Piece breakdown (PerFoot only) */}
      {product.pricingMode === 'PerFoot' && product.stockPieces && (
        <View style={styles.card}>
          <Text style={styles.sectionTitle}>Piece Breakdown</Text>
          <View style={styles.tableHeader}>
            <Text style={[styles.col, styles.colLeft,  styles.colHeader]}>Length (ft)</Text>
            <Text style={[styles.col, styles.colRight, styles.colHeader]}>Qty</Text>
            <Text style={[styles.col, styles.colRight, styles.colHeader]}>Total ft</Text>
          </View>
          {product.stockPieces.map((piece: StockPieceDto) => (
            <View key={piece.lengthFt} style={styles.tableRow}>
              <Text style={[styles.col, styles.colLeft]}>{piece.lengthFt}</Text>
              <Text style={[styles.col, styles.colRight]}>{piece.quantity}</Text>
              <Text style={[styles.col, styles.colRight]}>{piece.totalFeet.toFixed(2)}</Text>
            </View>
          ))}
          {product.stockPieces.length === 0 && (
            <Text style={styles.emptyPieces}>No pieces in stock.</Text>
          )}
        </View>
      )}
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  container:   { flex: 1, backgroundColor: Colors.background },
  content:     { padding: 12 },
  center:      { flex: 1, justifyContent: 'center', alignItems: 'center', padding: 24 },
  errorText:   { color: Colors.danger, fontSize: 15, textAlign: 'center' },

  card: {
    backgroundColor: Colors.surface, borderRadius: 12, padding: 16,
    marginBottom: 12, elevation: 2,
    shadowColor: '#000', shadowOpacity: 0.05, shadowRadius: 4,
    shadowOffset: { width: 0, height: 1 },
  },
  productName:  { fontSize: 20, fontWeight: '700', color: Colors.textPrimary },
  meta:         { fontSize: 14, color: Colors.textSecondary, marginTop: 4 },

  statsRow: { flexDirection: 'row', gap: 8, marginBottom: 12 },
  statBox: {
    flex: 1, backgroundColor: Colors.surface, borderRadius: 12, padding: 14,
    alignItems: 'center', elevation: 2,
    shadowColor: '#000', shadowOpacity: 0.05, shadowRadius: 4,
    shadowOffset: { width: 0, height: 1 },
  },
  statLabel:  { fontSize: 12, color: Colors.textSecondary, marginBottom: 4 },
  statValue:  { fontSize: 16, fontWeight: '700', color: Colors.textPrimary, textAlign: 'center' },

  sectionTitle: { fontSize: 16, fontWeight: '600', color: Colors.textPrimary, marginBottom: 12 },
  tableHeader:  { flexDirection: 'row', paddingBottom: 8, borderBottomWidth: 1, borderColor: Colors.divider },
  tableRow:     { flexDirection: 'row', paddingVertical: 8, borderBottomWidth: 1, borderColor: Colors.divider },
  col:          { fontSize: 14 },
  colLeft:      { flex: 1, color: Colors.textPrimary },
  colRight:     { width: 72, textAlign: 'right', color: Colors.textPrimary },
  colHeader:    { fontWeight: '600', color: Colors.textSecondary },
  emptyPieces:  { color: Colors.textHint, fontSize: 14, textAlign: 'center', paddingVertical: 12 },
});
