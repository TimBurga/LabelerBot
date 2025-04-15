using FishyFlip.Models;
using LabelerBot.Data;
using LabelerBot.Data.Entities;

namespace LabelerBot.Api;

public interface IBotService
{
    Task<IEnumerable<Subscriber>> GetSubscribers();
    Task<Subscriber> GetSubscriber();
    Task BackfillAll();
    Task Backfill(ATDid did);
    Task Reactivate(ATDid did);
    Task Deactivate(ATDid did);
    Task RestartBot();
    Task PruneExpired();
    Task Relabel(ATDid did);//?
    Task FindSubscriberRkey(ATDid did); //?
}

public class BotService(IDataRepository repo) : IBotService
{
    public async Task<IEnumerable<Subscriber>> GetSubscribers()
    {
        return await repo.GetActiveSubscribers();
    }

    public async Task<Subscriber> GetSubscriber()
    {
        throw new NotImplementedException();
    }

    public async Task BackfillAll()
    {
        throw new NotImplementedException();
    }

    public async Task Backfill(ATDid did)
    {
        throw new NotImplementedException();
    }

    public async Task Reactivate(ATDid did)
    {
        throw new NotImplementedException();
    }

    public async Task Deactivate(ATDid did)
    {
        throw new NotImplementedException();
    }

    public async Task RestartBot()
    {
        throw new NotImplementedException();
    }

    public async Task PruneExpired()
    {
        throw new NotImplementedException();
    }

    public async Task Relabel(ATDid did)
    {
        throw new NotImplementedException();
    }

    public async Task FindSubscriberRkey(ATDid did)
    {
        throw new NotImplementedException();
    }
}