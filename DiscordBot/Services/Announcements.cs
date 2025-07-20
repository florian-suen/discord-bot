using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NetCord.Rest;

namespace DISCORD_BOT.Services;

public interface IBirthday
{
    string Name { get; set; }
    DateTime Birthdate { get; set; }
    int DaysUntilNextBirthday();
    string GetBirthdayMessage();
}

public class BirthdayPerson : IBirthday
{
    public BirthdayPerson(string name, DateTime birthdate)
    {
        Name = name;
        Birthdate = birthdate;
    }

    public string Name { get; set; }
    public DateTime Birthdate { get; set; }


    public int DaysUntilNextBirthday()
    {
        var today = DateTime.Today;
        var nextBirthday = Birthdate.AddYears(today.Year - Birthdate.Year);

        if (nextBirthday < today) nextBirthday = nextBirthday.AddYears(1);

        return (nextBirthday - today).Days;
    }

    public string GetBirthdayMessage()
    {
        var daysUntil = DaysUntilNextBirthday();

        if (daysUntil == 0) return $"🎉 Happy Birthday, {Name}! 🎉 Today is your special day!";

        return $"Hello {Name}! " +
               $"Your next birthday is in {daysUntil} days.";
    }
}

public class Announcements(ILogger<Announcements> logger, RestClient client) : IHostedService, IDisposable
{
    private readonly IBirthday[] _birthday =
        [new BirthdayPerson("Suren", new DateTime(1945, 7, 20)), new BirthdayPerson("Suren", new DateTime())];

    private readonly Timer? _timer = null;

    public void Dispose()
    {
        _timer?.Dispose();
    }

    public Task StartAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Timed Hosted Service running.");

        DoWork(null);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Timed Hosted Service is stopping.");

        _timer?.Change(Timeout.Infinite, 0);

        return Task.CompletedTask;
    }

    private void DoWork(object? state)
    {
        var select = Array.Find(_birthday, birthday => birthday.Name == "Suren");

        // if (select is not null) client.SendMessageAsync(1368592085988413584, select.GetBirthdayMessage());
        logger.LogInformation(
            "Timed Hosted Service is working. Count");
    }
}