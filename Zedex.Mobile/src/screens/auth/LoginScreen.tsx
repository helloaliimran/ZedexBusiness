import React, { useEffect, useRef, useState } from 'react';
import {
  ActivityIndicator,
  Alert,
  KeyboardAvoidingView,
  Platform,
  Pressable,
  ScrollView,
  StyleSheet,
  Text,
  TextInput,
  View,
} from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { useAuth } from '../../context/AuthContext';
import { Colors } from '../../constants/colors';
import {
  checkBiometricCapability,
  isBiometricEnabled,
  promptBiometric,
  setBiometricEnabled,
} from '../../services/authService';

export function LoginScreen() {
  const { signIn } = useAuth();

  const [email,    setEmail]    = useState('');
  const [password, setPassword] = useState('');
  const [showPwd,  setShowPwd]  = useState(false);
  const [loading,  setLoading]  = useState(false);

  // Biometric quick-login (only shown when previously enabled)
  const [biometricAvailable, setBiometricAvailable] = useState(false);
  const passwordRef = useRef<TextInput>(null);

  useEffect(() => {
    (async () => {
      const [capability, enabled] = await Promise.all([
        checkBiometricCapability(),
        isBiometricEnabled(),
      ]);
      setBiometricAvailable(capability.isAvailable && enabled);
    })();
  }, []);

  const handleLogin = async () => {
    if (!email.trim() || !password) {
      Alert.alert('Validation', 'Please enter both email and password.');
      return;
    }
    setLoading(true);
    try {
      await signIn({ email: email.trim(), password });

      // First login — offer biometric setup if available
      const capability = await checkBiometricCapability();
      const alreadyEnabled = await isBiometricEnabled();
      if (capability.isAvailable && !alreadyEnabled) {
        Alert.alert(
          'Enable Biometric Login?',
          'Use your fingerprint or face to log in faster next time.',
          [
            { text: 'Not now', style: 'cancel' },
            {
              text: 'Enable',
              onPress: async () => {
                await setBiometricEnabled(true);
              },
            },
          ],
        );
      }
    } catch (err: any) {
      const msg =
        err?.response?.data?.message ??
        err?.message ??
        'Login failed. Please check your credentials.';
      Alert.alert('Login Failed', msg);
    } finally {
      setLoading(false);
    }
  };

  const handleBiometricLogin = async () => {
    const result = await promptBiometric();
    if (result.success) {
      // Biometric passed but we don't have fresh tokens yet —
      // fall through to password login (biometric is only for the "locked but tokens present" path)
      Alert.alert(
        'Biometric Verified',
        'Please enter your password once to restore your session.',
      );
    } else if (!result.cancelled) {
      Alert.alert('Biometric Failed', 'Could not verify your identity.');
    }
  };

  return (
    <KeyboardAvoidingView
      style={styles.flex}
      behavior={Platform.OS === 'ios' ? 'padding' : undefined}
    >
      <ScrollView
        contentContainerStyle={styles.container}
        keyboardShouldPersistTaps="handled"
      >
        {/* Logo / brand area */}
        <View style={styles.header}>
          <View style={styles.logoBox}>
            <Ionicons name="business" size={40} color={Colors.textOnPrimary} />
          </View>
          <Text style={styles.appName}>Zedex</Text>
          <Text style={styles.tagline}>Business Management</Text>
        </View>

        {/* Form card */}
        <View style={styles.card}>
          <Text style={styles.cardTitle}>Sign In</Text>

          {/* Email */}
          <View style={styles.fieldGroup}>
            <Text style={styles.label}>Email</Text>
            <TextInput
              style={styles.input}
              value={email}
              onChangeText={setEmail}
              keyboardType="email-address"
              autoCapitalize="none"
              autoCorrect={false}
              returnKeyType="next"
              onSubmitEditing={() => passwordRef.current?.focus()}
              placeholder="Enter your email"
              placeholderTextColor={Colors.textHint}
              editable={!loading}
            />
          </View>

          {/* Password */}
          <View style={styles.fieldGroup}>
            <Text style={styles.label}>Password</Text>
            <View style={styles.passwordRow}>
              <TextInput
                ref={passwordRef}
                style={[styles.input, styles.passwordInput]}
                value={password}
                onChangeText={setPassword}
                secureTextEntry={!showPwd}
                returnKeyType="done"
                onSubmitEditing={handleLogin}
                placeholder="Enter your password"
                placeholderTextColor={Colors.textHint}
                editable={!loading}
              />
              <Pressable
                onPress={() => setShowPwd(v => !v)}
                style={styles.eyeBtn}
                hitSlop={8}
              >
                <Ionicons
                  name={showPwd ? 'eye-off-outline' : 'eye-outline'}
                  size={20}
                  color={Colors.textSecondary}
                />
              </Pressable>
            </View>
          </View>

          {/* Sign in button */}
          <Pressable
            style={[styles.btn, loading && styles.btnDisabled]}
            onPress={handleLogin}
            disabled={loading}
          >
            {loading ? (
              <ActivityIndicator color={Colors.textOnPrimary} />
            ) : (
              <Text style={styles.btnText}>Sign In</Text>
            )}
          </Pressable>

          {/* Biometric quick-login (only when previously set up) */}
          {biometricAvailable && (
            <Pressable style={styles.biometricBtn} onPress={handleBiometricLogin}>
              <Ionicons name="finger-print" size={22} color={Colors.primary} />
              <Text style={styles.biometricText}>Use Biometric</Text>
            </Pressable>
          )}
        </View>
      </ScrollView>
    </KeyboardAvoidingView>
  );
}

const styles = StyleSheet.create({
  flex:      { flex: 1, backgroundColor: Colors.background },
  container: { flexGrow: 1, justifyContent: 'center', padding: 24 },

  header: { alignItems: 'center', marginBottom: 32 },
  logoBox: {
    width: 80, height: 80, borderRadius: 20,
    backgroundColor: Colors.primary,
    justifyContent: 'center', alignItems: 'center',
    marginBottom: 12,
  },
  appName:  { fontSize: 28, fontWeight: '700', color: Colors.textPrimary },
  tagline:  { fontSize: 14, color: Colors.textSecondary, marginTop: 4 },

  card: {
    backgroundColor: Colors.surface,
    borderRadius: 16,
    padding: 24,
    shadowColor: '#000',
    shadowOpacity: 0.08,
    shadowRadius: 8,
    shadowOffset: { width: 0, height: 2 },
    elevation: 3,
  },
  cardTitle: { fontSize: 20, fontWeight: '600', color: Colors.textPrimary, marginBottom: 20 },

  fieldGroup:   { marginBottom: 16 },
  label:        { fontSize: 13, fontWeight: '500', color: Colors.textSecondary, marginBottom: 6 },
  input: {
    borderWidth: 1, borderColor: Colors.border, borderRadius: 10,
    paddingHorizontal: 14, paddingVertical: 12,
    fontSize: 15, color: Colors.textPrimary, backgroundColor: Colors.surface,
  },
  passwordRow:  { flexDirection: 'row', alignItems: 'center' },
  passwordInput: { flex: 1 },
  eyeBtn:       { position: 'absolute', right: 14 },

  btn: {
    backgroundColor: Colors.primary, borderRadius: 10,
    paddingVertical: 14, alignItems: 'center', marginTop: 8,
  },
  btnDisabled: { opacity: 0.7 },
  btnText:     { color: Colors.textOnPrimary, fontSize: 16, fontWeight: '600' },

  biometricBtn: {
    flexDirection: 'row', alignItems: 'center', justifyContent: 'center',
    marginTop: 16, gap: 8,
  },
  biometricText: { color: Colors.primary, fontSize: 15, fontWeight: '500' },
});
