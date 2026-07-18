import { create } from 'zustand'

type UiState = { shellNoticeDismissed: boolean; dismissShellNotice: () => void }

export const useUiStore = create<UiState>((set) => ({
  shellNoticeDismissed: false,
  dismissShellNotice: () => set({ shellNoticeDismissed: true }),
}))
