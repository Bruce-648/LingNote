namespace LingNote;

public sealed class LibrarySection
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public bool Collapsed { get; set; }
}

public static class LibrarySections
{
    public static string TopicSection(Preferences prefs,string topic) =>
        prefs.TopicSections.TryGetValue(topic,out var id)&&prefs.Sections.Any(s=>s.Id==id)?id:"";
    public static string SectionOf(Preferences prefs,Course course) =>
        !string.IsNullOrEmpty(course.Topic)?TopicSection(prefs,course.Topic):
        prefs.Sections.Any(s=>s.Id==course.SectionId)?course.SectionId:"";
    public static void MoveRecord(Store store,Course course,string section)
    {
        course.Topic="";course.SectionId=section;store.Save(course);
    }
    public static void MoveTopic(Preferences prefs,string topic,string section)
    {
        if(string.IsNullOrEmpty(section))prefs.TopicSections.Remove(topic);
        else prefs.TopicSections[topic]=section;
        prefs.Save();
    }
    public static void DeleteRecordings(Store store,Preferences prefs,string section)
    {
        foreach(var course in store.All().Where(c=>SectionOf(prefs,c)==section))store.Delete(course);
    }
    public static void Remove(Store store,Preferences prefs,string section)
    {
        foreach(var course in store.All().Where(c=>c.SectionId==section)) {
            course.SectionId="";store.Save(course);
        }
        foreach(var topic in prefs.TopicSections.Where(p=>p.Value==section).Select(p=>p.Key).ToArray())
            prefs.TopicSections.Remove(topic);
        prefs.Sections.RemoveAll(s=>s.Id==section);
        if(prefs.ActiveSection==section)prefs.ActiveSection="";
        prefs.Save();
    }
}
