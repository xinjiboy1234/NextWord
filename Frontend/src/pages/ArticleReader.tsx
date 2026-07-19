import { ArrowLeft, BookOpenCheck } from 'lucide-react'
import { useEffect } from 'react'
import { ArticleText } from '../components/ArticleText'
import { CommentThread } from '../components/CommentThread'
import { VocabExtractPanel } from '../components/VocabExtractPanel'
import { WordPopover } from '../components/WordPopover'
import { useArticleReader } from '../hooks/useArticleReader'
import { useVocabExtract } from '../hooks/useVocabExtract'
import { useWordLookup } from '../hooks/useWordLookup'

interface ArticleReaderProps {
  articleId: string
  onBack: () => void
}

export function ArticleReader({ articleId, onBack }: ArticleReaderProps) {
  const reader = useArticleReader(articleId)
  const lookup = useWordLookup(articleId)
  const vocab = useVocabExtract(articleId)

  useEffect(() => {
    void vocab.loadExisting()
  }, [articleId])

  async function handleWordClick(word: string, paragraphIndex: number, paragraphText: string) {
    await lookup.lookup(word, paragraphText)
    await reader.recordLookup()
    void paragraphIndex
  }

  if (reader.loading) {
    return <p style={{ fontSize: 'var(--text-sm)', color: 'var(--muted)' }}>加载文章中...</p>
  }

  if (reader.error || !reader.article) {
    return <div className="alert alert-error">{reader.error ?? '文章不存在。'}</div>
  }

  return (
    <div className="reader-page">
      <div className="reader-toolbar row-between">
        <button type="button" onClick={onBack} className="btn btn-ghost btn-sm">
          <ArrowLeft size={16} aria-hidden="true" />
          返回文库
        </button>
        <button
          type="button"
          onClick={() => void reader.finishReading(reader.comments.length)}
          className="btn btn-primary btn-sm"
        >
          <BookOpenCheck size={16} aria-hidden="true" />
          完成阅读
        </button>
      </div>

      <div className={`reader-layout${lookup.selectedWord ? ' reader-layout--open' : ''}`}>
        <article className="reader-article card">
          <div className="row-between" style={{ flexWrap: 'wrap', marginBottom: 'var(--space-4)' }}>
            <div>
              <h2 style={{ fontFamily: 'var(--font-display)', fontSize: 'var(--text-xl)', fontWeight: 700 }}>
                {reader.article.title}
              </h2>
              <p style={{ marginTop: 'var(--space-1)', fontSize: 'var(--text-sm)', color: 'var(--muted)' }}>
                {reader.article.difficultyLevel} / {reader.article.cefrLevel} · {reader.article.wordCount} 词
                {reader.article.topicTag ? ` · ${reader.article.topicTag}` : ''}
              </p>
            </div>
            <p style={{ fontSize: 'var(--text-sm)', color: 'var(--muted)' }}>查词 {reader.lookupCount} 次</p>
          </div>

          <ArticleText
            content={reader.article.content}
            activeWord={lookup.selectedWord}
            onWordClick={(word, index, text) => void handleWordClick(word, index, text)}
          />
        </article>

        {lookup.selectedWord ? (
          <aside className="reader-sidebar">
            <WordPopover
              word={lookup.selectedWord}
              definition={lookup.definition}
              loading={lookup.loading}
              knownRate={lookup.lookupMeta?.estimatedKnownRate}
              personalDifficulty={lookup.lookupMeta?.personalDifficulty}
              onClose={lookup.clear}
            />
          </aside>
        ) : null}

        <div className="reader-extra stack stack-md">
          <VocabExtractPanel
            items={vocab.items}
            onWordSelect={(word) => void lookup.lookup(word)}
          />

          <CommentThread
            articleId={articleId}
            comments={reader.comments}
            onAdded={() => void reader.refreshComments()}
          />
        </div>
      </div>
    </div>
  )
}
