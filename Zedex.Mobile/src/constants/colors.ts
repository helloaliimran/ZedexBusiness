export const Colors = {
  // Brand
  primary:        '#1a73e8',
  primaryDark:    '#1557b0',
  primaryLight:   '#e8f0fe',

  // Semantic
  success:        '#1e8e3e',
  successLight:   '#e6f4ea',
  danger:         '#d93025',
  dangerLight:    '#fce8e6',
  warning:        '#f29900',
  warningLight:   '#fef7e0',

  // Balance colours (match web app)
  balanceOwes:    '#d93025', // positive balance → customer owes money
  balanceCredit:  '#1e8e3e', // zero/negative   → settled or in credit

  // Text
  textPrimary:    '#202124',
  textSecondary:  '#5f6368',
  textHint:       '#9aa0a6',
  textOnPrimary:  '#ffffff',

  // Backgrounds
  background:     '#f8f9fa',
  surface:        '#ffffff',
  surfaceVariant: '#f1f3f4',
  divider:        '#e8eaed',

  // Borders
  border:         '#dadce0',
  borderFocus:    '#1a73e8',

  // Invoice type badges
  badgeStandard:  '#e8f0fe',
  badgeStandardText: '#1a73e8',
  badgePvc:       '#fef7e0',
  badgePvcText:   '#b06000',
} as const;

export type ColorKey = keyof typeof Colors;
