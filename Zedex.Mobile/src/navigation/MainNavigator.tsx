import React from 'react';
import { createBottomTabNavigator } from '@react-navigation/bottom-tabs';
import { createNativeStackNavigator } from '@react-navigation/native-stack';
import { Ionicons } from '@expo/vector-icons';
import { Colors } from '../constants/colors';
import { MainTabParamList, StockStackParamList, BillsStackParamList, CustomersStackParamList } from '../types/navigation';

// ── Placeholder screens (Phase 4 & 5 will replace these) ─────────────────────
import { StockListScreen }    from '../screens/main/StockListScreen';
import { StockDetailScreen }  from '../screens/main/StockDetailScreen';
import { BillsListScreen }    from '../screens/main/BillsListScreen';
import { BillDetailScreen }   from '../screens/main/BillDetailScreen';
import { CustomerListScreen } from '../screens/main/CustomerListScreen';
import { CustomerLedgerScreen } from '../screens/main/CustomerLedgerScreen';

// ── Sub-stacks ────────────────────────────────────────────────────────────────

const StockStack = createNativeStackNavigator<StockStackParamList>();
function StockNavigator() {
  return (
    <StockStack.Navigator>
      <StockStack.Screen
        name="StockList"
        component={StockListScreen}
        options={{ title: 'Stock' }}
      />
      <StockStack.Screen
        name="StockDetail"
        component={StockDetailScreen}
        options={{ title: 'Product Detail' }}
      />
    </StockStack.Navigator>
  );
}

const BillsStack = createNativeStackNavigator<BillsStackParamList>();
function BillsNavigator() {
  return (
    <BillsStack.Navigator>
      <BillsStack.Screen
        name="BillsList"
        component={BillsListScreen}
        options={{ title: 'Bills' }}
      />
      <BillsStack.Screen
        name="BillDetail"
        component={BillDetailScreen}
        options={{ title: 'Bill Detail' }}
      />
    </BillsStack.Navigator>
  );
}

const CustomersStack = createNativeStackNavigator<CustomersStackParamList>();
function CustomersNavigator() {
  return (
    <CustomersStack.Navigator>
      <CustomersStack.Screen
        name="CustomerList"
        component={CustomerListScreen}
        options={{ title: 'Customers' }}
      />
      <CustomersStack.Screen
        name="CustomerLedger"
        component={CustomerLedgerScreen}
        options={({ route }) => ({ title: (route.params as any).customerName })}
      />
      <CustomersStack.Screen
        name="BillDetail"
        component={BillDetailScreen}
        options={{ title: 'Bill Detail' }}
      />
    </CustomersStack.Navigator>
  );
}

// ── Bottom tab navigator ──────────────────────────────────────────────────────

const Tab = createBottomTabNavigator<MainTabParamList>();

export function MainNavigator() {
  return (
    <Tab.Navigator
      screenOptions={({ route }) => ({
        headerShown: false,
        tabBarActiveTintColor:   Colors.primary,
        tabBarInactiveTintColor: Colors.textSecondary,
        tabBarStyle: { backgroundColor: Colors.surface, borderTopColor: Colors.border },
        tabBarIcon: ({ focused, color, size }) => {
          let iconName: keyof typeof Ionicons.glyphMap;
          if (route.name === 'StockTab') {
            iconName = focused ? 'cube' : 'cube-outline';
          } else if (route.name === 'BillsTab') {
            iconName = focused ? 'document-text' : 'document-text-outline';
          } else {
            iconName = focused ? 'people' : 'people-outline';
          }
          return <Ionicons name={iconName} size={size} color={color} />;
        },
      })}
    >
      <Tab.Screen
        name="StockTab"
        component={StockNavigator}
        options={{ tabBarLabel: 'Stock' }}
      />
      <Tab.Screen
        name="BillsTab"
        component={BillsNavigator}
        options={{ tabBarLabel: 'Bills' }}
      />
      <Tab.Screen
        name="CustomersTab"
        component={CustomersNavigator}
        options={{ tabBarLabel: 'Customers' }}
      />
    </Tab.Navigator>
  );
}
