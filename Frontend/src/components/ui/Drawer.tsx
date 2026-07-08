import { Dialog as BaseDialog } from '@base-ui/react/dialog'
import type { ReactNode } from 'react'

interface DrawerProps {
  open: boolean
  title: string
  onClose: () => void
  children: ReactNode
  footer?: ReactNode
}

export function Drawer({ open, title, onClose, children, footer }: DrawerProps) {
  return (
    <BaseDialog.Root open={open} onOpenChange={(next) => { if (!next) onClose() }}>
      <BaseDialog.Portal>
        <BaseDialog.Backdrop className={`drawer-overlay${open ? ' open' : ''}`} />
        <BaseDialog.Popup className={`drawer${open ? ' open' : ''}`}>
          <div className="drawer-header">
            <h2><BaseDialog.Title>{title}</BaseDialog.Title></h2>
            <BaseDialog.Close className="drawer-close" aria-label="关闭">
              ×
            </BaseDialog.Close>
          </div>
          <div className="drawer-body">{children}</div>
          {footer ? <div className="drawer-footer">{footer}</div> : null}
        </BaseDialog.Popup>
      </BaseDialog.Portal>
    </BaseDialog.Root>
  )
}
