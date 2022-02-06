using WordleOff.Shared.Games;

namespace WordleOff.Server.Hubs;

public enum AddPlayerResult
{
  Success,
  PlayerNameExist,
  PlayerMaxed,
  GameAlreadyStarted,
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
  private const Int32 GameSessionExpireMinutes = 1;
  private const Int32 ConnectionExpireSeconds = 8;
  private const Int32 PastAnswersMaxSize = 100;

  private DateTime? noPlayerSince = DateTime.Now;

  public String SessionId { get; set; } = "";
  public String CurrentAnswer { get { return pastAnswers.Last(); } }
  public Dictionary<String, PlayerData> PlayerDataDictionary { get; set; } = new();
  private readonly Queue<String> pastAnswers = new();
  public Boolean SessionExpired
  {
    get
    {
      DateTime now = DateTime.Now;
      TimeSpan noPlayerTimeSpan = now - (noPlayerSince ?? now);
      return TimeSpan.FromMinutes(GameSessionExpireMinutes) < noPlayerTimeSpan;
    }
  }  

  public GameSession(String sessionId, String? answer = null)
  {
    SessionId = sessionId;
    if (answer is null)
      answer = WordsService.NextRandomAnswer();
    pastAnswers.Enqueue(answer);
  }

  public void ResetGame()
  {
    String newAnswer;
    do
    {
      newAnswer = WordsService.NextRandomAnswer();
    } while (pastAnswers.Contains(newAnswer));
    pastAnswers.Enqueue(newAnswer);
    while (pastAnswers.Count > PastAnswersMaxSize)
      pastAnswers.Dequeue();

    foreach (var pair in PlayerDataDictionary)
      pair.Value.PlayData.Clear();
  }

  public AddPlayerResult AddPlayer(String connectionId, String newPlayerName)
  {
    if (PlayerDataDictionary.Count == MaxPlayers)
      return AddPlayerResult.PlayerMaxed;

    if (PlayerDataDictionary.Any(pair => pair.Value.PlayData.Count > 0))
      return AddPlayerResult.GameAlreadyStarted;

    if (PlayerDataDictionary.ContainsKey(newPlayerName))
      return AddPlayerResult.PlayerNameExist;
    
    Int32 maxIndex = PlayerDataDictionary.Count == 0 ? 0 : PlayerDataDictionary.Values.Max(x => x.Index);

    PlayerDataDictionary.Add(
      newPlayerName,
      new PlayerData() {
        Index = maxIndex + 1,
        ConnectionId = connectionId,
        PlayData = new(),
        DisconnectedDateTime = null
      }
    );
    noPlayerSince = null;
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
    var pairs = PlayerDataDictionary.Where(pair => pair.Value.ConnectionId == connectionId);
    if (pairs.Any())
    {
      var pair = pairs.First();
      pair.Value.DisconnectedDateTime = DateTime.Now;
    }
  }

  public Boolean RemoveDisconnectedPlayer()
  {
    DateTime now = DateTime.Now;
    
    var playerNamesToRemove = PlayerDataDictionary
      .Where((pair) => {
        TimeSpan disconnectedTimeSpan = now - (pair.Value.DisconnectedDateTime ?? now);
        return TimeSpan.FromSeconds(ConnectionExpireSeconds) < disconnectedTimeSpan;
      })
      .Select((pair) => pair.Key).ToList();

    foreach(String playerName in playerNamesToRemove)
      PlayerDataDictionary.Remove(playerName);
    if (PlayerDataDictionary.Count == 0 && playerNamesToRemove.Count > 0)
      noPlayerSince = now;
    return playerNamesToRemove.Count > 0;
  }

  public EnterWordResult EnterGuess(String playerName, String word)
  {
    if (!PlayerDataDictionary.ContainsKey(playerName))
      return EnterWordResult.PlayerNotFound;

    if (PlayerDataDictionary[playerName].PlayData.Count >= 6)
      return EnterWordResult.MaxGuesses;
    PlayerDataDictionary[playerName].PlayData.Add(word);
    return EnterWordResult.Success;
  }
}
