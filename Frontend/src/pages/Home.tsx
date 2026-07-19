import { BookOpen, RefreshCcw } from 'lucide-react'
import { useEffect, useMemo, useState } from 'react'
import { api } from '../api/client'
import { endpoints } from '../api/endpoints'
import { Badge } from '../components/ui/Badge'
import type { Word } from '../types/models'

interface HomeProps {
  onStart: () => void
}

export function Home({ onStart }: HomeProps) {
  const [words, setWords] = useState<Word[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [search, setSearch] = useState('')
  const [selectedId, setSelectedId] = useState<string | null>(null)

  async function load() {
    setLoading(true)
    setError(null)
    try {
      const response = await api.get<Word[]>(endpoints.words)
      setWords(response.data)
    } catch {
      setError('词库加载失败。')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    void load()
  }, [])

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase()
    if (!q) return words
    return words.filter((word) =>
      word.lemma.toLowerCase().includes(q)
      || word.meanings.some((m) => m.toLowerCase().includes(q)),
    )
  }, [words, search])

  const selected = words.find((w) => w.id === selectedId) ?? null

  return (
    <div className="wb-layout">
      <div>
        <div className="section-header row-between" style={{ flexWrap: 'wrap' }}>
          <div>
            <h2>词库</h2>
            <p>完整词条列表，点击单词查看详情。</p>
          </div>
          <button type="button" onClick={onStart} className="btn btn-primary btn-sm">
            <BookOpen size={16} aria-hidden="true" />
            开始学习
          </button>
        </div>

        <div style={{ marginBottom: 'var(--space-4)' }}>
          <input
            className="input"
            type="search"
            placeholder="搜索单词或释义…"
            value={search}
            onChange={(event) => setSearch(event.target.value)}
            autoComplete="off"
          />
        </div>

        {error ? <div className="alert alert-error">{error}</div> : null}
        {loading ? (
          <p style={{ color: 'var(--muted)', fontSize: 'var(--text-sm)' }}>加载中...</p>
        ) : (
          <table className="wb-table">
            <thead>
              <tr>
                <th>单词</th>
                <th>音标</th>
                <th>释义</th>
                <th>等级</th>
              </tr>
            </thead>
            <tbody>
              {filtered.map((word) => (
                <tr
                  key={word.id}
                  className={selectedId === word.id ? 'selected' : undefined}
                  onClick={() => setSelectedId(word.id)}
                >
                  <td className="wb-lemma">{word.lemma}</td>
                  <td className="wb-phonetic">{word.phonetics}</td>
                  <td>{word.meanings.join('；')}</td>
                  <td><Badge variant="muted">{word.cefrLevel}</Badge></td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      <aside className="side-panel">
        <div className="row-between">
          <h4 className="side-panel-title" style={{ margin: 0, border: 'none', padding: 0 }}>摘要</h4>
          <button type="button" onClick={() => void load()} className="btn btn-ghost btn-sm" aria-label="刷新词库">
            <RefreshCcw size={16} />
          </button>
        </div>
        <div style={{ fontFamily: 'var(--font-display)', fontSize: 'var(--text-3xl)', fontWeight: 700, marginTop: 'var(--space-4)' }}>
          {words.length}
        </div>
        <p style={{ fontSize: 'var(--text-sm)', color: 'var(--muted)' }}>总词条数</p>
        <div style={{ fontFamily: 'var(--font-display)', fontSize: 'var(--text-xl)', fontWeight: 700, marginTop: 'var(--space-4)' }}>
          {words.filter((w) => w.isCore).length}
        </div>
        <p style={{ fontSize: 'var(--text-sm)', color: 'var(--muted)' }}>核心词汇</p>

        <h4 className="side-panel-title" style={{ marginTop: 'var(--space-6)' }}>单词详情</h4>
        {selected ? (
          <div className="stack stack-sm" style={{ fontSize: 'var(--text-sm)' }}>
            <p style={{ fontFamily: 'var(--font-display)', fontSize: 'var(--text-lg)', fontWeight: 700 }}>{selected.lemma}</p>
            <p className="wb-phonetic">{selected.phonetics}</p>
            <p>{selected.meanings.join('；')}</p>
            <Badge variant="muted">{selected.difficultyLevel} / {selected.cefrLevel}</Badge>
          </div>
        ) : (
          <p style={{ fontSize: 'var(--text-sm)', color: 'var(--muted)' }}>点击左侧单词查看详情。</p>
        )}
      </aside>
    </div>
  )
}
