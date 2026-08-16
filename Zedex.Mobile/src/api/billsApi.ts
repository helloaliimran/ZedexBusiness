import { apiClient } from './client';
import { BillDetailDto, BillListItemDto, PagedResult } from '../types/api';
import { DEFAULT_PAGE_SIZE } from '../constants/config';

export interface GetBillsParams {
  type?:       'standard' | 'pvc';
  customerId?: number;
  search?:     string;
  from?:       string; // ISO date string
  to?:         string;
  page?:       number;
  pageSize?:   number;
}

export const billsApi = {
  getAll: async (params: GetBillsParams = {}): Promise<PagedResult<BillListItemDto>> => {
    const { data } = await apiClient.get<PagedResult<BillListItemDto>>('/api/bills', {
      params: { pageSize: DEFAULT_PAGE_SIZE, ...params },
    });
    return data;
  },

  getById: async (id: number): Promise<BillDetailDto> => {
    const { data } = await apiClient.get<BillDetailDto>(`/api/bills/${id}`);
    return data;
  },
};
