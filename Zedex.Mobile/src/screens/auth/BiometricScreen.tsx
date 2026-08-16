import React, { useEffect, useState } from 'react';
import {
  ActivityIndicator,
  Alert,
  Pressable,
  StyleSheet,
  Text,
  View,
} from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { useAuth } from '../../context/AuthContext';
import { Colors } from '../../constants/colors';
import { checkBiometricCapability, promptBiometric } from '../../services/authService';

export function BiometricScreen() {
  const { state, biometricSignIn, signOut } = useAuth();
  const [loading, setLoading] = useState(false);
  const [iconName, setIconName] = useState<'finger-print' | 'scan-outline'>('finger-print');

  useEffect(() => {
    (async () => {
      const { biometricType } = await checkBiometricCapability();
      setIconName(biometricType === 'face' ? 'scan-outline' : 'finger-print');
    })();
    // Auto-prompt on mount
    triggerBiometric();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const triggerBiometric = async () => {
    setLoading(true);
    try {
      const result = await promptBiometric();
      if (result.success) {
        const accessToken =
          state.status === 'biometric' ? state.accessToken : '';
        biometricSignIn(accessToken);
      } else if (!result.cancelled) {
        Alert.alert(
          'Authentication Failed',
          'Could not verify your identity. Try again or use your password.',
        );
      }
    } finally {
      setLoading(false);
    }
  };

  const handleUsePassword = async () => {
    await signOut(); // clears state → navigates to LoginScreen via AppNavigator
  };

  return (
    <View style={styles.container}>
      <View style={styles.logoBox}>
        <Ionicons name="business" size={40} color={Colors.textOnPrimary} />
      </View>
      <Text style={styles.appName}>Zedex</Text>
      <Text style={styles.subtitle}>Verify your identity to continue</Text>

      <Pressable
        style={styles.biometricCircle}
        onPress={triggerBiometric}
        disabled={loading}
      >
        {loading ? (
          <ActivityIndicator size="large" color={Colors.primary} />
        ) : (
          <Ionicons name={iconName} size={56} color={Colors.primary} />
        )}
      </Pressable>

      <Text style={styles.hint}>
        {loading ? 'Verifying…' : 'Tap to authenticate'}
      </Text>

      <Pressable style={styles.passwordBtn} onPress={handleUsePassword}>
        <Text style={styles.passwordBtnText}>Use Password Instead</Text>
      </Pressable>
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1, alignItems: 'center', justifyContent: 'center',
    backgroundColor: Colors.background, padding: 32,
  },
  logoBox: {
    width: 72, height: 72, borderRadius: 18,
    backgroundColor: Colors.primary,
    justifyContent: 'center', alignItems: 'center',
    marginBottom: 12,
  },
  appName: { fontSize: 26, fontWeight: '700', color: Colors.textPrimary },
  subtitle: {
    fontSize: 15, color: Colors.textSecondary,
    marginTop: 8, marginBottom: 48, textAlign: 'center',
  },
  biometricCircle: {
    width: 120, height: 120, borderRadius: 60,
    borderWidth: 2, borderColor: Colors.primaryLight,
    backgroundColor: Colors.surface,
    justifyContent: 'center', alignItems: 'center',
    shadowColor: Colors.primary,
    shadowOpacity: 0.15, shadowRadius: 12,
    elevation: 4,
  },
  hint: {
    marginTop: 20, fontSize: 14, color: Colors.textSecondary,
  },
  passwordBtn: { marginTop: 48 },
  passwordBtnText: {
    fontSize: 15, color: Colors.primary, fontWeight: '500',
  },
});
