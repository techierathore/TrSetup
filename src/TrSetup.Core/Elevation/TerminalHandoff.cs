namespace TrSetup.Core.Elevation;

/// <summary>
/// The WSL/Linux sudo handoff (REQ-FN-020 / REQ-NFR-002): TrSetup never asks for, reads,
/// stores or forwards a sudo password. Instead the UI renders this object — the one exact
/// command line for the user to paste into their own interactive terminal, where sudo
/// prompts them directly.
/// </summary>
/// <param name="CommandToPaste">The one-line command the user pastes (e.g. <c>sudo apt-get install -y socat</c>).</param>
/// <param name="Description">What the command does, for the UI caption.</param>
/// <param name="Instructions">Ready-to-render guidance telling the user to run the line in their own terminal.</param>
public sealed record TerminalHandoff(string CommandToPaste, string Description, string Instructions);
