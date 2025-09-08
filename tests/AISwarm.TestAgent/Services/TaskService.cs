using System.Collections.Concurrent;
using AISwarm.TestAgent.Models;

namespace AISwarm.TestAgent.Services;

public class TaskService : ITaskService
{
    private readonly ConcurrentDictionary<string, TestTask> _tasks = new();

    public ICollection<TestTask> GetTasks() => _tasks.Values;

    public ICollection<TestTask> GetPendingTasks() =>
        _tasks.Values.Where(t => t.Status == AISwarm.TestAgent.Models.TaskStatus.Pending).ToList();

    public TestTask? GetTask(string id) => _tasks.TryGetValue(id, out var task) ? task : null;

    public TestTask CreateTask(CreateTaskRequest request)
    {
        var task = new TestTask
        {
            Description = request.Description,
            Input = request.Input
        };
        _tasks[task.Id] = task;
        return task;
    }

    public TestTask? ClaimTask(string id, ClaimTaskRequest request)
    {
        if (!_tasks.TryGetValue(id, out var task) || task.Status != AISwarm.TestAgent.Models.TaskStatus.Pending)
            return null;

        task.Status = AISwarm.TestAgent.Models.TaskStatus.InProgress;
        task.ClaimedBy = request.AgentId;
        task.ClaimedAt = DateTime.UtcNow;
        return task;
    }

    public TestTask? CompleteTask(string id, CompleteTaskRequest request)
    {
        if (!_tasks.TryGetValue(id, out var task) || task.Status != AISwarm.TestAgent.Models.TaskStatus.InProgress)
            return null;

        task.Status = AISwarm.TestAgent.Models.TaskStatus.Completed;
        task.CompletedAt = DateTime.UtcNow;
        task.Output = request.Output;
        return task;
    }
}