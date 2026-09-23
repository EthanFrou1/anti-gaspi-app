import { Link, Stack } from 'expo-router';
import { StyleSheet, Text } from 'react-native';
import { APP_NAME } from '@/config';
import { colors } from '@/theme';

export default function AppLayout() {
  return (
    <Stack>
      <Stack.Screen
        name="index"
        options={{
          title: APP_NAME,
          headerRight: () => (
            <Link href="/account" style={styles.headerLink} accessibilityRole="button">
              <Text style={styles.headerLink}>Compte</Text>
            </Link>
          ),
        }}
      />
      <Stack.Screen name="account" options={{ title: 'Mon compte' }} />
    </Stack>
  );
}

const styles = StyleSheet.create({
  headerLink: { color: colors.primary, fontSize: 16, fontWeight: '600' },
});
