using Rnx.Abstractions.Tasks;
using static Rnx.Tasks.Core.Tasks;
using static Rnx.Tasks.Reliak.Markdown.Tasks;
using static Rnx.Tasks.Reliak.Minification.Tasks;

public class MyTasks
{
	ITaskDescriptor Layout => Series(
		ReadFiles("src/_layout.html"),
		Replace("{header}", ReadFiles("src/_header.html")),
		Replace("{footer}", ReadFiles("src/_footer.html"))
	);

	ITaskDescriptor MarkdownFiles => ReadFiles("src/**/*.md");
    

	public ITaskDescriptor Default => Series(
        Async(DeleteDir("wwwbuild")),
		MarkdownFiles,
		Markdown(),
		AsReplacementFor("{body}", Layout),
        Execute( e => System.Threading.Thread.Sleep(3000)),
		//HtmlMin(),
        Await(),
		WriteFiles("wwwbuild")
	);
}