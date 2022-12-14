namespace TestProject.Exp11.Folder2
{
    using SubjectWrong.Exp11.Folder1;

    public class MyClass1<RowT> : Typed1<Nested>
    {

    }
}

namespace TestProject.Exp11.Folder2
{
    public class MyClass2<RowT> : Typed2<SubjectWrong.Exp11.Folder1.Nested>
    {

    }
}