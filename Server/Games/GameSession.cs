using System.ComponentModel.DataAnnotations;
using WordleOff.Shared.Games;

namespace WordleOff.Server.Hubs;

public enum AddPlayerResult
{
  Success,
  ConnectionRestored,
  PlayerNameExist,
  PlayerMaxed,
  GameAlreadyStarted,
  CannotRestore,
  Unknown
}

public enum EnterWordResult
{
  Success,
  MaxGuesses,
  PlayerNotFound
}

public class GameSession
{
  private const Int32 MaxPlayers = 16;
  private const Int32 GameSessionExpireMinutes = 120;
  private const Int32 ConnectionExpireSeconds = 30;
  private const Int32 PastAnswersMaxSize = 50;

  [Key]
  public String SessionId { get; set; } = "";
  public Dictionary<String, PlayerData> PlayerDataDictionary { get; set; } = new();
  public Queue<String> PastAnswers { get; set; } = new();
  public DateTimeOffset? LastUpdateAt { get; set; } = DateTimeOffset.UtcNow;

  public String CurrentAnswer { get { return PastAnswers.Last(); } }
  public Boolean SessionExpired
  {
    get
    {
      DateTimeOffset now = DateTimeOffset.UtcNow;
      TimeSpan sinceLastUpdate = now - (LastUpdateAt ?? now);
      return TimeSpan.FromMinutes(GameSessionExpireMinutes) < sinceLastUpdate;
    }
  }  

  public GameSession() { } // Never Called

  public GameSession(String sessionId, String? answer = null)
  {
    SessionId = sessionId;
    if (answer is null)
      SetNewRandomAnswer();
    else
      PastAnswers.Enqueue(answer);
  }

  public void ResetGame()
  {
    SetNewRandomAnswer();

    foreach (var pair in PlayerDataDictionary)
      pair.Value.PlayData.Clear();

    LastUpdateAt = DateTimeOffset.UtcNow;
  }

  private void SetNewRandomAnswer()
  {
    String newAnswer;
    do
    {
      newAnswer = WordsService.NextRandomAnswer();
    } while (PastAnswers.Contains(newAnswer));
    PastAnswers.Enqueue(newAnswer);
    while (PastAnswers.Count > PastAnswersMaxSize)
      PastAnswers.Dequeue();
  }

  public AddPlayerResult AddPlayer(String connectionId, String clientGuid, String newPlayerName, Boolean restore)
  {
    if (PlayerDataDictionary.ContainsKey(newPlayerName))
    {
      if (PlayerDataDictionary[newPlayerName].ClientGuid == clientGuid)
      { // Restoring connection
        PlayerDataDictionary[newPlayerName].ConnectionId = connectionId;
        LastUpdateAt = DateTimeOffset.UtcNow;
        PlayerDataDictionary[newPlayerName].DisconnectedDateTime = null;
        return AddPlayerResult.ConnectionRestored;
      }
      else
        return AddPlayerResult.PlayerNameExist;
    }
    else if (restore)
      return AddPlayerResult.CannotRestore;

    if (PlayerDataDictionary.Count == MaxPlayers)
      return AddPlayerResult.PlayerMaxed;

    //if (PlayerDataDictionary.Any(pair => pair.Value.PlayData.Count > 0))
    //  return AddPlayerResult.GameAlreadyStarted;

    Int32 maxIndex = PlayerDataDictionary.Count == 0 ? 0 : PlayerDataDictionary.Values.Max(x => x.Index);

    PlayerDataDictionary.Add(
      newPlayerName,
      new PlayerData()
      {
        Index = maxIndex + 1,
        ConnectionId = connectionId,
        ClientGuid = clientGuid,
        PlayData = new(),
        DisconnectedDateTime = null
      }
    );
    LastUpdateAt = DateTimeOffset.UtcNow;
    return AddPlayerResult.Success;
  }

  public void ReconnectPlayer(String playerName, String newConnectionId)
  {
    if (!PlayerDataDictionary.ContainsKey(playerName))
      return;
    PlayerDataDictionary[playerName].ConnectionId = newConnectionId;
    PlayerDataDictionary[playerName].DisconnectedDateTime = null;
  }

  public void DisconnectPlayer(String connectionId)
  {
    DateTimeOffset now = DateTimeOffset.UtcNow;
    var pairs = PlayerDataDictionary.Where(pair => pair.Value.ConnectionId == connectionId);
    if (pairs.Any())
    {
      var pair = pairs.First();
      pair.Value.DisconnectedDateTime = now;
      LastUpdateAt = now;
    }
  }

  public void TreatAllPlayersAsDisconnected()
  { // This is useful when the server restarts and everyone needs to connect again
    DateTimeOffset now = DateTimeOffset.UtcNow;
    foreach (var pair in PlayerDataDictionary)
      pair.Value.DisconnectedDateTime = now;
    LastUpdateAt = now;
  }

  public Boolean RemoveDisconnectedPlayer()
  {
    DateTimeOffset now = DateTimeOffset.UtcNow;
    var playerNamesToRemove = PlayerDataDictionary
      .Where((pair) => {
        TimeSpan disconnectedTimeSpan = now - (pair.Value.DisconnectedDateTime ?? now);
        return TimeSpan.FromSeconds(ConnectionExpireSeconds) < disconnectedTimeSpan;
      })
      .Select((pair) => pair.Key).ToList();
    foreach (String playerName in playerNamesToRemove)
      PlayerDataDictionary.Remove(playerName);
    if (PlayerDataDictionary.Count == 0 && playerNamesToRemove.Count > 0)
    {
      SetNewRandomAnswer();
      LastUpdateAt = now;
    }
    return playerNamesToRemove.Count > 0;
  }

  public EnterWordResult EnterGuess(String playerName, String word)
  {
    LastUpdateAt = DateTimeOffset.UtcNow;

    if (!PlayerDataDictionary.ContainsKey(playerName))
      return EnterWordResult.PlayerNotFound;

    if (PlayerDataDictionary[playerName].PlayData.Count >= 6)
      return EnterWordResult.MaxGuesses;
    PlayerDataDictionary[playerName].PlayData.Add(word);
    return EnterWordResult.Success;
  }
}
