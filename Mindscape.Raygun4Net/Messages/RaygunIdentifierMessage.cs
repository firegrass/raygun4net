﻿namespace Mindscape.Raygun4Net.Messages
{
  public class RaygunIdentifierMessage
  {
    public RaygunIdentifierMessage(string user)
    {
      Identifier = user;
    }

    public string Identifier { get; private set; }
  }
}
