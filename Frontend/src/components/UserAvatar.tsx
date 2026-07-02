interface UserAvatarProps {
  displayName: string
  active?: boolean
  onClick?: () => void
}

function getInitials(name: string) {
  const trimmed = name.trim()
  if (!trimmed) return '?'
  const parts = trimmed.split(/\s+/).filter(Boolean)
  if (parts.length >= 2) {
    return `${parts[0][0]}${parts[1][0]}`.toUpperCase()
  }
  return trimmed.slice(0, 2).toUpperCase()
}

export function UserAvatar({ displayName, active = false, onClick }: UserAvatarProps) {
  const initials = getInitials(displayName)

  return (
    <button
      type="button"
      onClick={onClick}
      aria-label="我的"
      title={displayName}
      className="sidebar-avatar"
      style={{
        width: 44,
        height: 44,
        fontSize: 'var(--text-sm)',
        border: active ? '2px solid var(--fg)' : '2px solid transparent',
        boxShadow: active ? '0 0 0 2px var(--border)' : undefined,
      }}
    >
      {initials}
    </button>
  )
}
