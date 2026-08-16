import { apiClient } from './client';
import { StockProductDto } from '../types/api';

export const stockApi = {
  getAll: async (params?: {
    search?:     string;
    categoryId?: number;
  }): Promise<StockProductDto[]> => {
    const { data } = await apiClient.get<StockProductDto[]>('/api/stock', { params });
    return data;
  },

  getById: async (id: number): Promise<StockProductDto> => {
    const { data } = await apiClient.get<StockProductDto>(`/api/stock/${id}`);
    return data;
  },
};
