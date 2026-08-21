import axios from 'axios';
import { CHAT_BASE_URL, CHAT_TIMEOUT_MS } from '../constants/config';
import { ChatRequest, ChatResponse } from '../types/api';

// Dedicated client for the LLM/assistant backend. Deliberately separate from
// the authenticated apiClient: the /chat endpoint is a plain FastAPI server and
// does NOT expect a Bearer token. If you later protect it, add the token here.
export const chatClient = axios.create({
  baseURL: CHAT_BASE_URL,
  timeout: CHAT_TIMEOUT_MS,
  headers: { 'Content-Type': 'application/json' },
});

export const chatApi = {
  /**
   * Send the user's latest message plus the running conversation history.
   * customer_id stays null until a customer context is bound to the session.
   */
  sendMessage: async (payload: ChatRequest): Promise<ChatResponse> => {
    const { data } = await chatClient.post<ChatResponse>('/chat', payload);
    return data;
  },
};