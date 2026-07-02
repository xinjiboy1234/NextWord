import { useNavigate, useParams } from 'react-router-dom'
import { ArticleReader } from './ArticleReader'

export function ArticleReaderRoute() {
  const { articleId } = useParams<{ articleId: string }>()
  const navigate = useNavigate()

  if (!articleId) {
    return null
  }

  return (
    <ArticleReader
      articleId={articleId}
      onBack={() => navigate('/reading')}
    />
  )
}
