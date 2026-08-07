using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Audio;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

public static class Restarter
{
    public static async Task RestartRoom(CancellationToken token = default)
    {
        if (RunManager.Instance.NetService.Type != NetGameType.Singleplayer)
            return;

        if (!SaveManager.Instance.HasRunSave)
            return;

        token.ThrowIfCancellationRequested();

        RunManager.Instance.ActionQueueSet.Reset();

        NRunMusicController.Instance.StopMusic();

        RunManager.Instance.CleanUp();

        SerializableRun saveData =
            SaveManager.Instance.LoadRunSave().SaveData;

        RunState runState =
            RunState.FromSerializable(saveData);

        RunManager.Instance.SetUpSavedSingleplayer(
            runState,
            saveData
        );

        SfxCmd.Play(
            runState.Players[0]
                .Character
                .CharacterTransitionSfx
        );

        NGame.Instance.ReactionContainer.InitializeNetworking(
            new NetSingleplayerGameService()
        );

        // Wait until loading has actually completed.
        await NGame.Instance.LoadRun(
            runState,
            saveData.PreFinishedRoom
        );

        token.ThrowIfCancellationRequested();
    }
}