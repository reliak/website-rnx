# Concepts

## Tasks
Rnx is a task runner, so the central concept is a task. A task is a single unit of work that does only one thing,
but it does it well. A task has a `Name` property and an `Execute` method:
```
public interface ITask
{
	string Name {get; }
	void Execute(IBuffer input, IBuffer output, IExecutionContext executionContext);
}
```

One example of a task would be a `ReadFiles` task, that reads all files from a user-specified directory.
Of course this task alone would be of no purpose, so Rnx allows you to connect multiple tasks with each other,
where each task uses the output of the previous task as its input. The input and output of a task can be seen
from the above `Execute` method header. We will address the topic of the data flow between tasks later.

##  Task descriptors
As stated above, tasks will contain the main execution logic to solve a single problem.
However, when interacting with Rnx you will never use tasks directly, you will always use *task descriptors*.
For every task there must exist a task descriptor, e.g. for a `ReadFilesTask` there would be a `ReadFilesTaskDescriptor`.
A task descriptor has two purposes. First, it defines the type of the actual task that should be executed. Second,
a task descriptor collects all user-defined information that the actual task will need to run properly.

You might ask yourself why a task descriptor is required at all, when the user-defined information could also be specified
in the task itself. This is a valid concern. The reason for this additional level of indirection is that if users
would reference tasks directly they would also need to instantiate a task class. This might seem ok at first, but
what if a task has dependencies to other services that must be specified in the constructor of the task?
Then a user would have to specify these dependencies when creating a task. With task descriptors, users do not need to know what dependencies
the actual task has. Rnx is responsible for instantiating a task and for injecting the needed dependencies.

Example: Lets say we have the following task descriptor:
```
public class ReadFilesTaskDescriptor : ITaskDescriptor
{
	public string Directory { get; }
	
	public ReadFilesTaskDescriptor(string directory)
	{
		Directory = directory;
	}

	public Type TaskType => typeof(ReadFilesTask);
}
```
The above task descriptor defines the `ReadFilesTask` as the task type. This task is shown below:
```
public class ReadFilesTask : ITask
{
	public string Name => "ReadFiles";
	
	public ReadFilesTask(ReadFilesTaskDescriptor taskDescriptor, IFileSystem fileSystem)
	{
	}
	
	public void Execute(IBuffer input, IBuffer output, IExecutionContext executionContext)
	{
		// ...
	}
}
```

For the above scenario, a user would only specify `new ReadFilesTaskDescriptor("[some directory]")`. Rnx later analyzes this task descriptor,
creates a new instance of `ReadFilesTask` and injects the task descriptor and all other dependencies that it can resolve.

## Data flow between tasks
The data flow within Rnx follows the producer-consumer pattern, with typically (but not necessarily) one producer and always one consumer.
The `Execute`-method of a task recieves two instances of `IBuffer`. One instance is the input buffer and one instance is the output buffer.
Among other members, the `IBuffer` interface has an `Add`-method and an `Elements`-property:
```
public interface IBuffer
{
	IEnumerable<IBufferElement> Elements { get; }
	void Add(IBufferElement element);
	
	// more members
}
```
A task uses the `Elements`-property of the input buffer to iterate over all elements. Note that the call to the `Elements`-property will
block until new elements (produced by the previous task) are available. This means that as soon as a new element is available,
the current task can start with processing this element.

Example:
```
// imagine the previous task has not produced any elements so far
// the below enumerator from input.Elements will wait until the first element arrives
// when the previous task notifies the buffer that all elements were added and we iterated through all
// these elements, then the foreach-loop will exit
foreach(var element in input.Elements)
{
	// process this element
}

// at this point all elements from the previous task were processed and the previous task has signaled
// the no more elements are available
```

The `Add`-method from the output buffer is used to add elements to the next processing task, i.e. the output buffer of a task is the input buffer of the subsequent task. For the above `ReadFilesTask` this could look like this:
```
public void Execute(IBuffer input, IBuffer output, IExecutionContext executionContext)
{
	// we do not use input buffer here
	
	foreach(var filename in Directory.EnumerateFiles("..."))
	{
		// create a buffer element from the file content
		IBufferElement newElement = new BufferElement(File.ReadAllText(filename));
		
		// add this element to the output buffer
		output.Add(newElement);
	}
}
```
Now imagine there is a `ReplaceTask` that executes right after the above task. The `Execute`-method of this task could look like this:
```
public void Execute(IBuffer input, IBuffer output, IExecutionContext executionContext)
{
	// iterate over all elements from the input buffer (this is the output buffer of the ReadFilesTask)
	foreach(var element in input.Elements)
	{
		// replace some text
		element.Text = element.Text.Replace("{someplaceholder}", "somevalue");
		
		// add the modified element to the output buffer
		output.Add(element);
	}
}
```
Note: If you do not add an element to the output buffer, it is lost forever and can't be processed by any subsequent task.
This can be used purposely to filter out elements that are not required anymore. Note also that iterating over the `Elements`-property removes an element, i.e. after iterating through all `input.Elements` the input buffer will be empty.

You typically will want to iterate over `input.Elements` as shown above, so that each element can be processed as soon as it becomes available.
But sometimes you might need all elements before you can start processing, for example when you want to concat the text of all elements.
Then it is ok to use `var elements = input.Elements.ToArray()`. This will block until all elements are processed by the previous task.
