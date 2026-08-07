import { useEffect, useState } from 'react';
import { StyleSheet, Text, View } from 'react-native';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import { residencesApi } from '../../api/residencesApi';
import type { AppApiError } from '../../api/httpClient';
import { useSessionStore } from '../../auth/sessionStore';
import { useDevicesStore } from '../../store/devicesStore';
import type { RootStackParamList } from '../../navigation/types';
import { colors } from '../../theme/colors';
import { fonts, typography } from '../../theme/typography';
import { IconButton } from '../../ui/IconButton';
import { PrimaryButton } from '../../ui/PrimaryButton';
import { Screen } from '../../ui/Screen';
import { TextField } from '../../ui/TextField';
import { appAlert } from '../../ui/dialogStore';

type Props = NativeStackScreenProps<RootStackParamList, 'ResidenceForm'>;

export function ResidenceFormScreen({ route, navigation }: Props) {
  const residenceId = route.params?.residenceId;
  const isEdit = !!residenceId;
  const queryClient = useQueryClient();
  const user = useSessionStore((s) => s.user);
  const setResidence = useDevicesStore((s) => s.setResidence);
  const selectedResidenceId = useDevicesStore((s) => s.selectedResidenceId);

  const existingQuery = useQuery({
    queryKey: ['residence', residenceId],
    enabled: isEdit,
    queryFn: () => residencesApi.get(residenceId!),
  });

  const [name, setName] = useState('');
  const [address, setAddress] = useState('');
  const [timezone, setTimezone] = useState('America/Sao_Paulo');
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!existingQuery.data) return;
    setName(existingQuery.data.name);
    setAddress(existingQuery.data.address ?? '');
    setTimezone(existingQuery.data.timezone || 'America/Sao_Paulo');
  }, [existingQuery.data]);

  const saveMutation = useMutation({
    mutationFn: async () => {
      if (!user?.tenantId) throw { message: 'Tenant inválido' } as AppApiError;
      if (isEdit) {
        return residencesApi.update(residenceId!, {
          name,
          timezone,
          address: address || null,
        });
      }
      return residencesApi.create(user.tenantId, { name, timezone, address });
    },
    onSuccess: async (residence) => {
      setError(null);
      await queryClient.invalidateQueries({ queryKey: ['residences'] });
      setResidence(residence.id, user?.userId);
      navigation.goBack();
    },
    onError: (err: AppApiError) => setError(err.message || 'Falha ao salvar residência'),
  });

  const deleteMutation = useMutation({
    mutationFn: () => residencesApi.remove(residenceId!),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['residences'] });
      if (selectedResidenceId === residenceId) {
        setResidence(null, user?.userId);
      }
      navigation.goBack();
    },
    onError: (err: AppApiError) => appAlert('Erro', err.message || 'Falha ao remover'),
  });

  const confirmDelete = () => {
    appAlert('Remover residência', 'Esta ação não pode ser desfeita no app.', [
      { text: 'Cancelar', style: 'cancel' },
      {
        text: 'Remover',
        style: 'destructive',
        onPress: () => deleteMutation.mutate(),
      },
    ]);
  };

  return (
    <Screen scroll>
      <View style={styles.topRow}>
        <IconButton
          name="chevron-back"
          accessibilityLabel="Voltar"
          onPress={() => navigation.goBack()}
        />
        <Text style={styles.topLabel}>Residência</Text>
        <View style={{ width: 44 }} />
      </View>

      <Text style={styles.title}>{isEdit ? 'Editar residência' : 'Nova residência'}</Text>
      <Text style={styles.hint}>Organize portões e acessos por local.</Text>

      <TextField label="Nome" placeholder="Casa principal" value={name} onChangeText={setName} />
      <TextField
        label="Endereço (opcional)"
        placeholder="Rua, cidade…"
        value={address}
        onChangeText={setAddress}
      />
      <TextField
        label="Timezone"
        placeholder="America/Sao_Paulo"
        value={timezone}
        onChangeText={setTimezone}
        autoCapitalize="none"
      />

      {error ? <Text style={styles.error}>{error}</Text> : null}

      <PrimaryButton
        label={isEdit ? 'Salvar' : 'Criar residência'}
        variant="brand"
        loading={saveMutation.isPending}
        disabled={!name.trim()}
        onPress={() => saveMutation.mutate()}
        style={{ marginTop: 18 }}
      />

      {isEdit ? (
        <PrimaryButton
          label="Remover residência"
          variant="danger"
          loading={deleteMutation.isPending}
          onPress={confirmDelete}
          style={{ marginTop: 12 }}
        />
      ) : null}
    </Screen>
  );
}

const styles = StyleSheet.create({
  topRow: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    marginBottom: 12,
  },
  topLabel: { ...typography.label, color: colors.inkMuted, textTransform: 'uppercase' },
  title: {
    fontFamily: fonts.display,
    fontSize: 30,
    color: colors.brand,
    letterSpacing: -0.6,
  },
  hint: { ...typography.caption, color: colors.inkMuted, marginTop: 6, marginBottom: 8 },
  error: { color: colors.danger, marginTop: 12, fontFamily: fonts.sansSemi },
});
