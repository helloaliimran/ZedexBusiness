import { apiClient } from './client';
import { CustomerSummaryDto, LedgerResponseDto } from '../types/api';
import { DEFAULT_PAGE_SIZE } from '../constants/config';

export interface GetLedgerParams {
  from?:     string; // ISO date string
  to?:       string;
  page?:     number;
  pageSize?: number;
}

export const customersApi = {
  getAll: async (search?: string): Promise<CustomerSummaryDto[]> => {
    const { data } = await apiClient.get<CustomerSummaryDto[]>('/api/customers', {
      params: search ? { search } : undefined,
    });
    return data;
  },

  getLedger: async (
    customerId: number,
    params: GetLedgerParams = {},
  ): Promise<LedgerResponseDto> => {
    const { data } = await apiClient.get<LedgerResponseDto>(
      `/api/customers/${customerId}/ledger`,
      { params: { pageSize: DEFAULT_PAGE_SIZE, ...params } },
    );
    return data;
  },
};
