﻿namespace WordleOff.Shared.Games;

public enum ServerJoinError
{
  SessionNotFound,
  NameTaken,
  SessionFull,
  SessionInProgress,
  CannotRestore,
}