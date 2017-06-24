using System;
using System.Diagnostics;
using System.Threading;
using Pooler;

class Program {

    const int RUNNING_THREADS_IN_TEST_TOTAL = 3000;
    const int RUNNING_THREADS_IN_TEST_SIMULTANEOUSLY = 50;

	private static Pooler.Base _pool;
    private static System.Diagnostics.Stopwatch _stopWatch;
    private static System.Threading.Timer _reportTimer;

    private static int _taskBeginCounter = 0;
    private static int _taskExceptionCounter = 0;
    private static int _taskDoneCounter = 0;
    private static int _currentlyRunningTasksCounter = 0;
    private static int _peakThreadsCounter = 0;

    private static object _taskBeginCounterLock = new object { };
    private static object _taskExceptionCounterLock = new object { };
    private static object _taskDoneCounterLock = new object { };

    static void Main (string[] args) {
		// init threads pool for parallel pool accepting each task as different call:
		Program._initParallelThreadsPool();

		// init threads pool for repeater pool accepting each task as the same call:
		//Program._initRepeaterThreadsPool();

		// start the test:
		Program._pool.StartProcessing();
        
        Thread.Sleep(2000);
        int origValue = Program._pool.GetMaxRunningTasks();
		Program._pool.SetMaxRunningTasks(200);

        Thread.Sleep(2000);
		Program._pool.SetMaxRunningTasks(origValue);
        
        Console.ReadLine();
    }

	private static void _initParallelThreadsPool () {
		Parallel pool = Parallel.CreateNew(Program.RUNNING_THREADS_IN_TEST_SIMULTANEOUSLY);
		pool.AllDone += Program._allDoneHandler;
		pool.TaskDone += Program._taskDoneHandler;
		pool.TaskException += Program._threadExceptionHandler;
		for (int i = 0; i < Program.RUNNING_THREADS_IN_TEST_TOTAL; i++) {
			pool.Add(new Pooler.TaskDelegate(Program._testTask), false, ThreadPriority.Lowest);
		}
		Thread.CurrentThread.Priority = ThreadPriority.Highest;
		Program._stopWatch = Stopwatch.StartNew();
		Program._reportTimer = new System.Threading.Timer(delegate {
			Thread.CurrentThread.Priority = ThreadPriority.Highest;
			Program._report();
		}, null, 0, 200);
		Program._pool = pool;
	}

	private static void _initRepeaterThreadsPool () {
		Repeater pool = Repeater.CreateNew(
			Program.RUNNING_THREADS_IN_TEST_SIMULTANEOUSLY,
			Program.RUNNING_THREADS_IN_TEST_TOTAL
		);
		pool.AllDone += Program._allDoneHandler;
		pool.TaskDone += Program._taskDoneHandler;
		pool.TaskException += Program._threadExceptionHandler;
		pool.Set(new Pooler.TaskDelegate(Program._testTask), false, ThreadPriority.Lowest, false);

		Thread.CurrentThread.Priority = ThreadPriority.Highest;
		Program._stopWatch = Stopwatch.StartNew();
		Program._reportTimer = new System.Threading.Timer(delegate {
			Thread.CurrentThread.Priority = ThreadPriority.Highest;
			Program._report();
		}, null, 0, 200);
		Program._pool = pool;
	}

    private static void _taskDoneHandler (Pooler.Base pool, TaskDoneEventArgs poolerTaskDoneEventArgs) {
        lock (Program._taskDoneCounterLock) {
            Program._taskDoneCounter++;
            Program._currentlyRunningTasksCounter = poolerTaskDoneEventArgs.RunningTasksCount;

		}
    }
    private static void _threadExceptionHandler (Pooler.Base pool, ExceptionEventArgs poolThreadExceptionEventArgs) {
        lock (Program._taskExceptionCounterLock) {
            Program._taskExceptionCounter++;
        }
    }
    private static void _allDoneHandler (Pooler.Base pool, AllDoneEventArgs poolAllDoneEventArgs) {
        Program._peakThreadsCounter = poolAllDoneEventArgs.PeakThreadsCount;
        if (poolAllDoneEventArgs.Exceptions.Count != Program._taskExceptionCounter) {
            throw new Exception("Exceptions counter is wrong!");
        }
        Program._report();
    }
	private static void _testTask (Pooler.Base pool) {
		var i = 0;
		lock (Program._taskBeginCounterLock) {
			i = Program._taskBeginCounter;
			Program._taskBeginCounter++;
		}
		Thread.Sleep(10);
		if (i % 3 == 0) {
			throw new Exception($"Exception modulo 3");
		}
	}

	private static void _report () {
        int[] counts = new int[] { 0, 0, 0, 0 };
        lock (Program._taskBeginCounterLock) {
            counts[0] = Program._taskBeginCounter;
        }
        lock (Program._taskDoneCounterLock) {
            counts[1] = Program._taskDoneCounter;
            counts[2] = Program._currentlyRunningTasksCounter;
        }
        lock (Program._taskExceptionCounterLock) {
            counts[3] = Program._taskExceptionCounter;
        }

		string s = "Started tasks: " + counts[0] + Environment.NewLine
			+ "Finished tasks: " + counts[1] + Environment.NewLine
			+ "Threads exceptions: " + counts[3] + Environment.NewLine
			+ "Currently simultaneously running threads: " + counts[2] + Environment.NewLine
			+ "Maximum simultaneously runing threads peak: " + Program._peakThreadsCounter + Environment.NewLine
			+ "Spended miliseconds: " + Program._stopWatch.ElapsedMilliseconds;

        Console.Clear();
        Console.WriteLine(s);
        if (counts[1] == Program.RUNNING_THREADS_IN_TEST_TOTAL) {
            Program._reportTimer.Dispose();
        }
    }
}