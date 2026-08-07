import { Modal, Pressable, StyleSheet, Text, View } from 'react-native';
import { colors } from '../theme/colors';
import { fonts, typography } from '../theme/typography';
import { radii } from '../theme/spacing';
import { PrimaryButton } from './PrimaryButton';
import { useDialogStore, type AppDialogButton } from './dialogStore';

function buttonVariant(
  style: AppDialogButton['style'],
  index: number,
  total: number,
): 'accent' | 'brand' | 'danger' | 'muted' | 'ghost' {
  if (style === 'destructive') return 'danger';
  if (style === 'cancel') return 'muted';
  // Único botão ou ação principal
  if (total === 1 || index === total - 1) return 'brand';
  return 'muted';
}

export function AppDialog() {
  const dialog = useDialogStore((s) => s.dialog);
  const hide = useDialogStore((s) => s.hide);

  const buttons = dialog?.buttons ?? [];
  const cancelOnBackdrop = buttons.some((b) => b.style === 'cancel') || buttons.length === 1;

  const onPressButton = (button: AppDialogButton) => {
    hide();
    // defer para o Modal fechar antes de ações de navegação/Alert encadeado
    requestAnimationFrame(() => {
      button.onPress?.();
    });
  };

  return (
    <Modal
      visible={!!dialog}
      transparent
      animationType="fade"
      statusBarTranslucent
      onRequestClose={() => {
        if (cancelOnBackdrop) hide();
      }}
    >
      <Pressable
        style={styles.backdrop}
        onPress={() => {
          if (cancelOnBackdrop) hide();
        }}
      >
        <Pressable style={styles.card} onPress={(e) => e.stopPropagation()}>
          <View style={styles.accentBar} />
          <Text style={styles.title}>{dialog?.title}</Text>
          {dialog?.message ? <Text style={styles.message}>{dialog.message}</Text> : null}
          <View style={styles.actions}>
            {buttons.map((button, index) => (
              <PrimaryButton
                key={`${button.text}-${index}`}
                label={button.text}
                variant={buttonVariant(button.style, index, buttons.length)}
                onPress={() => onPressButton(button)}
                style={styles.actionBtn}
              />
            ))}
          </View>
        </Pressable>
      </Pressable>
    </Modal>
  );
}

const styles = StyleSheet.create({
  backdrop: {
    flex: 1,
    backgroundColor: 'rgba(10, 28, 23, 0.48)',
    alignItems: 'center',
    justifyContent: 'center',
    paddingHorizontal: 28,
  },
  card: {
    width: '100%',
    maxWidth: 360,
    backgroundColor: colors.bgElevated,
    borderRadius: radii.xl,
    paddingHorizontal: 22,
    paddingTop: 20,
    paddingBottom: 18,
    borderWidth: 1,
    borderColor: colors.border,
    shadowColor: colors.shadow,
    shadowOpacity: 1,
    shadowRadius: 24,
    shadowOffset: { width: 0, height: 12 },
    elevation: 12,
  },
  accentBar: {
    width: 40,
    height: 3,
    borderRadius: radii.pill,
    backgroundColor: colors.brand,
    marginBottom: 16,
  },
  title: {
    fontFamily: fonts.display,
    fontSize: 24,
    color: colors.brand,
    letterSpacing: -0.4,
  },
  message: {
    ...typography.body,
    color: colors.inkMuted,
    marginTop: 8,
    lineHeight: 22,
  },
  actions: {
    marginTop: 22,
    gap: 10,
  },
  actionBtn: {
    minHeight: 48,
  },
});
