import { create } from 'zustand';

export type AppDialogButton = {
  text: string;
  style?: 'default' | 'cancel' | 'destructive';
  onPress?: () => void;
};

export type AppDialogPayload = {
  title: string;
  message?: string;
  buttons: AppDialogButton[];
};

type DialogState = {
  dialog: AppDialogPayload | null;
  show: (payload: AppDialogPayload) => void;
  hide: () => void;
};

export const useDialogStore = create<DialogState>((set) => ({
  dialog: null,
  show: (payload) => set({ dialog: payload }),
  hide: () => set({ dialog: null }),
}));

/** API compatível com Alert.alert — diálogos no visual Domus. */
export function appAlert(
  title: string,
  message?: string,
  buttons?: AppDialogButton[],
): void {
  useDialogStore.getState().show({
    title,
    message,
    buttons: buttons?.length ? buttons : [{ text: 'OK' }],
  });
}
