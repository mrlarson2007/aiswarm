using AISwarm.TestAgent.Models;

namespace AISwarm.TestAgent.Services;

public interface ITaskService
{
    ICollection<TestTask> GetTasks();
    ICollection<TestTask> GetPendingTasks();
    TestTask? GetTask(string id);
    TestTask CreateTask(CreateTaskRequest request);
    TestTask? ClaimTask(string id, ClaimTaskRequest request);
    TestTask? CompleteTask(string id, CompleteTaskRequest request);
}
