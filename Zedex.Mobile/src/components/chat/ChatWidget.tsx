import React, { useCallback, useEffect, useRef, useState } from 'react';
import {
  ActivityIndicator,
  KeyboardAvoidingView,
  ScrollView,
  StyleSheet,
  Text,
  TextInput,
  View,
  Pressable,
  Platform,
} from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { Colors } from '../../constants/colors';
import { chatApi } from '../../api/chatApi';
import { ChatHistoryMessage } from '../../types/api';
import { useAuth } from '../../context/AuthContext';

// Empty-state suggestions shown before the user types anything.
const SUGGESTIONS = [
  'Find a product',
  'Show stock levels',
  'Help create a bill',
];

function renderSuggestions(onSend: (text: string) => void) {
  return (
    <View style={styles.suggestions}>
      <Text style={styles.greeting}>Hi! How can I help you today?</Text>
      {SUGGESTIONS.map(s => (
        <Pressable
          key={s}
          style={styles.suggestionChip}
          onPress={() => onSend(s)}
        >
          <Ionicons name="sparkles-outline" size={14} color={Colors.primary} />
          <Text style={styles.suggestionText}>{s}</Text>
        </Pressable>
      ))}
    </View>
  );
}

function renderBubble(m: ChatHistoryMessage, index: number) {
  const isUser = m.role === 'user';
  return (
    <View
      key={`${index}-${m.role}`}
      style={[styles.bubble, isUser ? styles.bubbleUser : styles.bubbleBot]}
    >
      <Text style={[styles.bubbleText, isUser ? styles.bubbleTextUser : styles.bubbleTextBot]}>
        {m.content}
      </Text>
    </View>
  );
}

function renderTyping() {
  return (
    <View key="typing" style={[styles.bubble, styles.bubbleBot]}>
      <View style={styles.typingRow}>
        <ActivityIndicator size="small" color={Colors.primary} />
        <Text style={styles.bubbleTextBot}>…</Text>
      </View>
    </View>
  );
}

export function ChatWidget() {
  const insets = useSafeAreaInsets();
  const { state } = useAuth();
  const [open, setOpen] = useState(false);
  const [messages, setMessages] = useState<ChatHistoryMessage[]>([]);
  const [input, setInput] = useState('');
  const [sending, setSending] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const scrollRef = useRef<ScrollView>(null);

  // Auto-scroll to the newest message whenever the history or loading changes.
  useEffect(() => {
    scrollRef.current?.scrollToEnd();
  }, [messages, sending]);

  const toggleChat = useCallback(() => setOpen(v => !v), []);

  const send = useCallback(async (text?: string) => {
    const trimmed = (text ?? input).trim();
    if (!trimmed || sending) return;

    // Keep server-authoritative history: optimistically append the user bubble,
    // call the API, then replace the list with the server's returned history.
    setInput('');
    setError(null);
    setSending(true);

    const optimistic = [...messages, { role: 'user' as const, content: trimmed }];
    setMessages(optimistic);

    try {
      const res = await chatApi.sendMessage({
        message: trimmed,
        history: messages, // the PREVIOUS history; server appends new turns
        customer_id: null, // no customer bound to the session yet
      });
      if (res.history && res.history.length > 0) {
        setMessages(res.history);
      } else {
        setMessages([...optimistic, { role: 'assistant', content: res.reply }]);
      }
    } catch {
      const reply =
        'Sorry, I could not reach the assistant server. ' +
        'Check that the chat backend is running and CHAT_BASE_URL is reachable.';
      setMessages([...optimistic, { role: 'assistant', content: reply }]);
      setError('Assistant is unavailable right now.');
    } finally {
      setSending(false);
    }
  }, [messages, sending, input]);

  const clearChat = useCallback(() => {
    setMessages([]);
    setError(null);
  }, []);

  const fabBottom = 16 + insets.bottom;

  // Only show the floating assistant for signed-in users.
  if (state.status !== 'authenticated') return null;

  return (
    <>
      {/* Floating action button */}
      <Pressable
        style={[styles.fab, { bottom: fabBottom }]}
        onPress={toggleChat}
        accessibilityLabel={open ? 'Close chat' : 'Open chat'}
      >
        <Ionicons
          name={open ? 'close' : 'chatbubbles'}
          size={26}
          color={Colors.textOnPrimary}
        />
      </Pressable>

      {/* Chat window */}
      {open && (
        <KeyboardAvoidingView
          style={styles.sheet}
          behavior={Platform.OS === 'ios' ? 'padding' : undefined}
        >
          {/* Header */}
          <View style={styles.header}>
            <View style={styles.botDot} />
            <Text style={styles.title} numberOfLines={1}>Zedex Assistant</Text>
            <Pressable onPress={clearChat} accessibilityLabel="Clear chat" hitSlop={8} style={styles.iconBtn}>
              <Ionicons name="trash-outline" size={19} color={Colors.textSecondary} />
            </Pressable>
            <Pressable onPress={toggleChat} accessibilityLabel="Close chat" hitSlop={8} style={styles.iconBtn}>
              <Ionicons name="close" size={22} color={Colors.textSecondary} />
            </Pressable>
          </View>

          {/* Messages */}
          <ScrollView
            ref={scrollRef}
            style={styles.body}
            contentContainerStyle={styles.messageList}
            keyboardShouldPersistTaps="handled"
          >
            {messages.length === 0
              ? renderSuggestions(send)
              : (
                <>
                  {messages.map(renderBubble)}
                  {sending && renderTyping()}
                </>
              )}
          </ScrollView>

          {/* Error hint */}
          {error && (
            <View style={styles.errorRow}>
              <Text style={styles.errorText}>{error}</Text>
            </View>
          )}

          {/* Input bar */}
          <View style={styles.inputBar}>
            <TextInput
              style={styles.input}
              value={input}
              onChangeText={setInput}
              placeholder="Ask about stock, bills, products…"
              placeholderTextColor={Colors.textHint}
              returnKeyType="send"
              editable={!sending}
              onSubmitEditing={() => send()}
            />
            <Pressable
              style={[styles.sendBtn, (!input.trim() || sending) && styles.sendBtnDisabled]}
              onPress={() => send()}
              disabled={!input.trim() || sending}
              accessibilityLabel="Send message"
            >
              {sending
                ? <ActivityIndicator size="small" color={Colors.textOnPrimary} />
                : <Ionicons name="send" size={20} color={Colors.textOnPrimary} />}
            </Pressable>
          </View>
        </KeyboardAvoidingView>
      )}
    </>
  );
}

const styles = StyleSheet.create({
  // Floating button
  fab: {
    position: 'absolute', right: 16,
    width: 58, height: 58, borderRadius: 29,
    backgroundColor: Colors.primary,
    justifyContent: 'center', alignItems: 'center',
    elevation: 6,
    shadowColor: '#000', shadowOpacity: 0.25, shadowRadius: 6,
    shadowOffset: { width: 0, height: 3 },
  },

  // Chat window sheet
  sheet: {
    position: 'absolute',
    left: 8, right: 8, bottom: 88,
    height: 480,
    maxWidth: 420, alignSelf: 'center',
    backgroundColor: Colors.surface,
    borderRadius: 20,
    borderWidth: 1, borderColor: Colors.border,
    elevation: 8,
    shadowColor: '#000', shadowOpacity: 0.3, shadowRadius: 10,
    shadowOffset: { width: 0, height: 4 },
    overflow: 'hidden',
  },

  header: {
    flexDirection: 'row', alignItems: 'center',
    paddingHorizontal: 14, paddingVertical: 12, gap: 8,
    backgroundColor: Colors.primaryLight,
    borderBottomWidth: 1, borderColor: Colors.divider,
  },
  botDot: {
    width: 10, height: 10, borderRadius: 5,
    backgroundColor: Colors.success,
  },
  title: { flex: 1, fontSize: 16, fontWeight: '700', color: Colors.primaryDark },
  iconBtn: { padding: 4 },

  body: { flex: 1, backgroundColor: Colors.background },
  messageList: { paddingHorizontal: 12, paddingVertical: 12, gap: 8 },

  bubble: {
    maxWidth: '88%',
    paddingHorizontal: 12, paddingVertical: 9,
    borderRadius: 14,
  },
  bubbleUser: {
    alignSelf: 'flex-end',
    backgroundColor: Colors.primary,
    borderBottomRightRadius: 4,
  },
  bubbleBot: {
    alignSelf: 'flex-start',
    backgroundColor: Colors.surfaceVariant,
    borderBottomLeftRadius: 4,
  },
  bubbleText:      { fontSize: 14, lineHeight: 20 },
  bubbleTextUser:  { color: Colors.textOnPrimary },
  bubbleTextBot:   { color: Colors.textPrimary },

  typingRow: { flexDirection: 'row', alignItems: 'center', gap: 6 },

  // Empty state
  suggestions: { padding: 16, alignItems: 'center', gap: 16 },
  greeting: { fontSize: 15, color: Colors.textSecondary, textAlign: 'center' },
  suggestionChip: {
    flexDirection: 'row', alignItems: 'center',
    backgroundColor: Colors.surface,
    borderRadius: 20, paddingHorizontal: 16, paddingVertical: 10,
    borderWidth: 1, borderColor: Colors.border,
  },
  suggestionText: { marginLeft: 6, fontSize: 13, color: Colors.primary },

  errorRow: {
    backgroundColor: Colors.dangerLight,
    paddingHorizontal: 12, paddingVertical: 6, marginBottom: 4,
    borderRadius: 8, alignSelf: 'center',
  },
  errorText: { color: Colors.danger, fontSize: 12, textAlign: 'center' },

  // Input bar
  inputBar: {
    flexDirection: 'row', alignItems: 'center', gap: 8,
    paddingHorizontal: 10, paddingVertical: 8,
    backgroundColor: Colors.surface,
    borderTopWidth: 1, borderColor: Colors.divider,
  },
  input: {
    flex: 1,
    backgroundColor: Colors.surfaceVariant,
    borderRadius: 18,
    paddingHorizontal: 14, paddingVertical: 8,
    fontSize: 15, color: Colors.textPrimary,
    maxHeight: 90,
  },
  sendBtn: {
    width: 44, height: 44, borderRadius: 22,
    backgroundColor: Colors.primary,
    justifyContent: 'center', alignItems: 'center',
  },
  sendBtnDisabled: { opacity: 0.4 },
});