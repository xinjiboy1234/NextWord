using NextWord.Domain.Entities;
using NextWord.Domain.Enums;

namespace NextWord.Infrastructure.Data;

/// <summary>
/// 内置短题库种子数据。每篇约 120-150 词，按难度分级。
/// </summary>
public static class ArticleSeedData
{
    public static IEnumerable<Article> CreateArticles()
    {
        foreach (var item in BasicArticles())
        {
            yield return item;
        }

        foreach (var item in IntermediateArticles())
        {
            yield return item;
        }

        foreach (var item in AdvancedArticles())
        {
            yield return item;
        }
    }

    private static IEnumerable<Article> BasicArticles()
    {
        yield return Create("My Morning Routine", "life",
            """
            Every morning I wake up at seven o'clock. First, I wash my face and brush my teeth. Then I eat breakfast with my family. We usually have bread, eggs, and milk. After breakfast, I put on my school clothes and pack my bag. I walk to school with my friend. The walk takes about fifteen minutes. At school, I say hello to my teacher and classmates. I like mornings because they feel fresh and quiet. A good morning helps me learn better during the day.
            """,
            DifficultyLevel.Basic, CefrLevel.A1);

        yield return Create("A Day at the Park", "life",
            """
            On Sunday, my family goes to the park near our home. The park has green trees, flowers, and a small lake. Children play on the swings and slides. My little brother likes to feed the ducks. I bring a book and sit on a bench to read. Sometimes we buy ice cream from a cart. In the afternoon, we walk around the lake and take photos. The air smells clean and the birds sing in the trees. We go home when the sun starts to go down. It is a simple and happy day.
            """,
            DifficultyLevel.Basic, CefrLevel.A1);

        yield return Create("Learning to Cook", "life",
            """
            Last month I started learning to cook simple meals. My mother taught me how to make fried rice. First, I cut vegetables into small pieces. Then I heat oil in a pan and cook the vegetables. After that, I add rice and eggs. I mix everything with a spoon and add a little salt. The kitchen smells good when the food is ready. Cooking is not easy at first, but practice helps. Now I can make lunch for myself when my parents are busy. I feel proud when my family says the food tastes nice.
            """,
            DifficultyLevel.Basic, CefrLevel.A2);

        yield return Create("My Favorite Season", "nature",
            """
            My favorite season is autumn. The weather is cool and comfortable. Leaves on the trees turn red, yellow, and orange. I like walking on streets covered with fallen leaves. In autumn, we eat warm soup and drink hot tea at home. Schools start again after the long summer holiday. I enjoy seeing my friends and sharing stories about our vacations. Autumn also has a holiday when families visit each other. The sky looks clear and blue on many days. For me, autumn is a season of change and new beginnings.
            """,
            DifficultyLevel.Basic, CefrLevel.A2);

        yield return Create("A Trip to the Library", "school",
            """
            Yesterday I went to the city library with my class. The building is big and quiet inside. A librarian showed us how to find books with a computer. I borrowed a story about a young traveler. The library has many sections for children, science, and history. I sat at a table and read for one hour. Other students were reading or doing homework. Before we left, I returned an old book and chose two new ones. The librarian reminded us to bring the books back on time. I want to visit the library again next week.
            """,
            DifficultyLevel.Basic, CefrLevel.A2);

        yield return Create("Helping at Home", "life",
            """
            On weekends I help my parents with housework. I clean my room and wash the dishes after dinner. Sometimes I water the plants on the balcony. My father teaches me to fold clothes neatly. My mother says teamwork makes our home more comfortable. At first I did not like cleaning, but now I understand it is part of family life. When everyone helps, we finish faster and have more time to relax together. I also learn responsibility by taking care of my own things. Small actions every day can make a big difference at home.
            """,
            DifficultyLevel.Basic, CefrLevel.A1);

        yield return Create("Our School Sports Day", "school",
            """
            Last Friday was our school sports day. Students wore team colors and ran races on the field. I joined the relay race with three classmates. We practiced passing a baton many times before the event. My heart beat fast when our turn came. We did not win first place, but we tried our best. Teachers gave water and cheered for every team. After the races, we ate lunch together outside. Some students played football while others rested under trees. Sports day was tiring but fun. I learned that trying together is more important than winning alone.
            """,
            DifficultyLevel.Basic, CefrLevel.A2);

        yield return Create("Buying Fresh Fruit", "life",
            """
            Near my home there is a small market that opens early every morning. Farmers bring fresh fruit and vegetables from nearby villages. My mother and I go there on Saturdays. We look for apples, bananas, and oranges that look bright and firm. The seller weighs the fruit and puts it in paper bags. I like the sweet smell of ripe peaches in summer. We talk with the sellers and learn which fruits are in season. Buying local food supports people in our community. After shopping, we walk home and make a fruit salad for lunch.
            """,
            DifficultyLevel.Basic, CefrLevel.A1);

        yield return Create("My Pet Cat", "life",
            """
            I have a pet cat named Luna. She is gray and white with soft fur. Luna sleeps on my bed during the day and plays at night. She likes to chase a small ball across the floor. Every morning I feed her and change her water. Luna sometimes sits on my desk when I do homework. She is quiet but very curious about new objects. When I feel tired, Luna comes near and purrs. Taking care of a pet teaches patience and kindness. Luna is not just an animal in our house; she is part of our family.
            """,
            DifficultyLevel.Basic, CefrLevel.A1);

        yield return Create("Rainy Day Indoors", "life",
            """
            When it rains heavily, we stay indoors and find quiet activities. I listen to the sound of rain on the window and read a book. My sister draws pictures at the table. My father makes tea and plays soft music in the living room. We do not feel bored because we plan small projects together. Sometimes we bake simple cookies in the kitchen. Rainy days remind us to slow down and enjoy time at home. The streets outside look shiny and empty. When the rain stops, the air feels fresh and cool. I like both sunny days and peaceful rainy afternoons.
            """,
            DifficultyLevel.Basic, CefrLevel.A2);
    }

    private static IEnumerable<Article> IntermediateArticles()
    {
        yield return Create("Balancing Study and Rest", "school",
            """
            Many students struggle to balance study hours with proper rest. When people sleep too little, their memory becomes weaker and learning feels harder the next day. Regular breaks during study sessions help the brain process new information. Some learners use a timer to study for twenty-five minutes and then rest for five. Others prefer longer blocks with a short walk between tasks. Exercise, healthy meals, and conversation with friends also support mental energy. Teachers suggest reviewing notes briefly before bed instead of starting new topics late at night. A balanced routine does not mean studying less; it means studying more effectively over time.
            """,
            DifficultyLevel.Intermediate, CefrLevel.B1);

        yield return Create("How Cities Reduce Waste", "environment",
            """
            Modern cities face growing pressure to reduce waste and protect public health. Local governments encourage residents to separate plastic, paper, and food waste into different bins. Some communities offer collection points for batteries and old electronics. Schools teach children to reuse bottles and avoid single-use bags. Markets may charge a small fee for plastic packaging to change consumer habits. Recycling centers process materials and send them to factories for new products. Although these systems require investment, they lower pollution and save resources in the long term. Citizen participation remains essential because rules only work when people follow them daily.
            """,
            DifficultyLevel.Intermediate, CefrLevel.B1);

        yield return Create("The Value of Habit", "life",
            """
            A habit is a repeated action that eventually feels automatic. People who build small daily habits often achieve larger goals without constant motivation. For language learners, reading ten minutes each evening can strengthen vocabulary more reliably than occasional long sessions. The key is consistency rather than intensity at the beginning. Tracking progress in a simple notebook can reveal patterns and encourage continuation. When a habit becomes part of routine, it requires less willpower to maintain. However, habits can also be negative, such as checking a phone before sleep. Replacing one habit with another takes time, but clear cues and rewards make change possible.
            """,
            DifficultyLevel.Intermediate, CefrLevel.B1);

        yield return Create("Working in Teams", "work",
            """
            Teamwork is common in schools and workplaces because complex tasks require different skills. Effective teams communicate clearly about goals, deadlines, and responsibilities. When one member faces difficulty, others can offer support or share knowledge. Conflict may appear, but respectful discussion often leads to better decisions. Leaders help coordinate tasks, yet good teams also listen to quiet members. Digital tools allow remote groups to share documents and track progress in real time. Successful collaboration depends on trust, punctuality, and willingness to accept feedback. Learning to work in teams prepares students for future careers where independent effort alone is rarely enough.
            """,
            DifficultyLevel.Intermediate, CefrLevel.B2);

        yield return Create("Travel Memories", "culture",
            """
            Traveling to a new region can broaden a person's view of culture and daily life. Visitors notice differences in food, transportation, and social customs. These experiences become strong memories because they combine novelty with personal emotion. Some travelers keep journals to record small details that photos cannot capture. Others collect local phrases and try to use them in conversation. Travel also teaches adaptability when plans change due to weather or delays. Returning home, many people compare their routines with what they observed abroad. Thoughtful travel is not only about famous landmarks; it is about paying attention to how people live and communicate.
            """,
            DifficultyLevel.Intermediate, CefrLevel.B2);

        yield return Create("Digital Privacy Basics", "technology",
            """
            Digital privacy has become an important concern for students and professionals who use online services daily. Applications often request access to contacts, location, or camera functions. Users should read permission settings carefully and disable features they do not need. Strong passwords and two-step verification reduce the risk of account theft. Public Wi-Fi networks may expose personal data, so sensitive tasks are safer on trusted connections. Schools and companies also store information that requires legal protection. Understanding privacy helps people make informed choices rather than accepting every default option. Responsible digital behavior protects both individual users and the wider community.
            """,
            DifficultyLevel.Intermediate, CefrLevel.B2);

        yield return Create("Preparing for Presentations", "school",
            """
            Presentations challenge learners to organize ideas and speak clearly in front of others. Preparation usually begins with a narrow topic and a simple outline. Speakers benefit from practicing aloud to identify unclear sentences and awkward timing. Visual slides should support the message rather than repeat every word. Eye contact and a steady pace help the audience stay engaged. Nervousness is normal, but deep breathing before speaking can reduce tension. Classmates may ask questions that reveal gaps in explanation, which is useful feedback. With repeated practice, students gain confidence and learn to express complex information in accessible language.
            """,
            DifficultyLevel.Intermediate, CefrLevel.B1);
    }

    private static IEnumerable<Article> AdvancedArticles()
    {
        yield return Create("The Science of Memory", "academic",
            """
            Cognitive researchers distinguish between short-term storage and long-term retention when studying human memory. Short-term memory handles immediate tasks, while long-term memory supports knowledge accumulated over weeks or years. Retrieval practice, which requires learners to recall information without looking at notes, often produces stronger retention than passive review. Spacing study sessions across days further improves outcomes because the brain consolidates material during rest. Emotional significance can also influence what people remember vividly. Educators apply these findings by designing curricula that combine repetition, context, and meaningful application. Understanding memory mechanisms allows learners to adopt strategies that align with biological constraints rather than relying on ineffective cramming alone.
            """,
            DifficultyLevel.Advanced, CefrLevel.C1);

        yield return Create("Ethics in Artificial Intelligence", "academic",
            """
            Artificial intelligence systems increasingly influence hiring, healthcare, and public communication. Ethical debates focus on transparency, accountability, and potential bias embedded in training data. When models reproduce historical inequalities, affected communities may experience unfair treatment without clear avenues for appeal. Developers therefore emphasize documentation of data sources and evaluation across diverse groups. Regulation remains uneven across countries, creating uncertainty for international companies. Some organizations adopt internal review boards to assess high-risk applications before deployment. Critics argue that technical efficiency should not override human dignity or informed consent. Responsible innovation requires collaboration among engineers, policymakers, and the public.
            """,
            DifficultyLevel.Advanced, CefrLevel.C1);

        yield return Create("Urban Architecture and Identity", "culture",
            """
            Architecture shapes how residents perceive identity and belonging within a city. Historic districts preserve material evidence of earlier social values, while contemporary towers signal economic ambition and global connection. Planners must negotiate space for housing, commerce, green areas, and transportation networks. Poor design can isolate neighborhoods or reduce pedestrian safety, whereas thoughtful layouts encourage interaction and cultural exchange. Public consultation has become more common, allowing citizens to challenge projects that ignore local context. Sustainable materials and energy-efficient structures now influence major developments. Ultimately, urban architecture expresses collective priorities about memory, progress, and the quality of everyday life.
            """,
            DifficultyLevel.Advanced, CefrLevel.C1);

        yield return Create("Climate Policy Challenges", "environment",
            """
            Climate policy involves scientific evidence, economic trade-offs, and political negotiation across regions with unequal resources. Nations that industrialized early contributed significant historical emissions, while emerging economies seek development opportunities that depend on energy consumption. International agreements attempt to coordinate targets, yet enforcement mechanisms remain limited. Carbon pricing, renewable subsidies, and investment in public transit represent complementary strategies with different political costs. Local communities experience climate impacts unevenly, from drought to flooding, which intensifies calls for adaptive infrastructure. Effective policy must therefore combine mitigation with resilience planning. Long-term success depends on credible commitments rather than symbolic declarations alone.
            """,
            DifficultyLevel.Advanced, CefrLevel.C2);
    }

    private static Article Create(string title, string topic, string content, DifficultyLevel level, CefrLevel cefr)
    {
        var wordCount = content.Split([' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries).Length;
        return new Article
        {
            Title = title,
            Content = content.Trim(),
            TopicTag = topic,
            DifficultyLevel = level,
            CefrLevel = cefr,
            WordCount = wordCount,
            Source = ArticleSource.Builtin
        };
    }
}
