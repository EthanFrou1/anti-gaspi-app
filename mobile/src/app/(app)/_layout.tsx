import { Stack } from 'expo-router';
import { APP_NAME } from '@/config';

export default function AppLayout() {
  return (
    <Stack>
      <Stack.Screen name="index" options={{ title: APP_NAME }} />
    </Stack>
  );
}
