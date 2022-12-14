namespace TestProject.Exp11.Folder2
{
    public abstract class Typed1<RowT> : SubjectWrong.Exp11.Folder1.Nested
        where RowT : SubjectWrong.Exp11.Folder1.Nested
    {

    }
}

namespace TestProject.Exp11.Folder2
{
    public abstract class Typed2<RowT> : SubjectWrong.Exp11.Folder1.Nested
        where RowT : SubjectWrong.Exp11.Folder1.Nested
    {

    }
}
