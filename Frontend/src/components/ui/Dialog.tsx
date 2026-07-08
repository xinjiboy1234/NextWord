import { Dialog as BaseDialog } from '@base-ui/react/dialog'
import type { ReactNode } from 'react'
import { Button } from './Button'

interface AlertDialogProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  title: string
  description: string
  confirmLabel?: string
  cancelLabel?: string
  onConfirm: () => void
  loading?: boolean
}

export function AlertDialog({
  open,
  onOpenChange,
  title,
  description,
  confirmLabel = '确认',
  cancelLabel = '取消',
  onConfirm,
  loading = false,
}: AlertDialogProps) {
  return (
    <BaseDialog.Root open={open} onOpenChange={onOpenChange}>
      <BaseDialog.Portal>
        <BaseDialog.Backdrop className="dialog-backdrop" />
        <BaseDialog.Popup className="dialog-popup card">
          <BaseDialog.Title className="dialog-title">{title}</BaseDialog.Title>
          <BaseDialog.Description className="dialog-description">
            {description}
          </BaseDialog.Description>
          <div className="dialog-actions">
            <Button variant="ghost" onClick={() => onOpenChange(false)} disabled={loading}>
              {cancelLabel}
            </Button>
            <Button
              variant="primary"
              disabled={loading}
              onClick={() => { onConfirm() }}
            >
              {loading ? '处理中...' : confirmLabel}
            </Button>
          </div>
        </BaseDialog.Popup>
      </BaseDialog.Portal>
    </BaseDialog.Root>
  )
}

interface DialogShellProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  title: string
  children: ReactNode
  footer?: ReactNode
}

export function DialogShell({
  open,
  onOpenChange,
  title,
  children,
  footer,
}: DialogShellProps) {
  return (
    <BaseDialog.Root open={open} onOpenChange={onOpenChange}>
      <BaseDialog.Portal>
        <BaseDialog.Backdrop className="dialog-backdrop" />
        <BaseDialog.Popup className="dialog-popup card dialog-popup-lg">
          <div className="dialog-header row-between">
            <BaseDialog.Title className="dialog-title">{title}</BaseDialog.Title>
            <BaseDialog.Close className="drawer-close" aria-label="关闭">
              ×
            </BaseDialog.Close>
          </div>
          <div className="dialog-body">{children}</div>
          {footer ? <div className="dialog-footer">{footer}</div> : null}
        </BaseDialog.Popup>
      </BaseDialog.Portal>
    </BaseDialog.Root>
  )
}
