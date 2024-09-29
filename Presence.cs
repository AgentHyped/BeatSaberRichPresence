using IPA;
using IPA.Config;
using IPA.Config.Stores;
using DiscordRPC;
using IPALogger = IPA.Logging.Logger;
using DataPuller.Data;
using System;
using UnityEngine;

namespace BeatSaberRichPresence 
{
    [Plugin(RuntimeOptions.SingleStartInit)]
    public class Presence
    {
        internal static Presence? Instance { get; private set; }
        internal static IPALogger? Log { get; private set; }
        private DiscordRpcClient ?client;

        [Init]
        public Presence(IPALogger logger, Config config)
        {
            Instance = this;
            Log = logger;
        }

        [OnStart]
        public void OnApplicationStart()
        {
            Log!.Info("Beat Saber Rich Presence Plugin Loaded!");
        
            try
            {
                client = new DiscordRpcClient("1289691163389067325");
                client.Initialize();
                Log.Info("DiscordRPC initialized successfully.");

                MainMenuPresence("Main Menu");

                LiveData.Instance.OnUpdate += UpdatePresence;
                MapData.Instance.OnUpdate += UpdatePresence;
            }
            catch (Exception ex)
            {
                Log.Error($"Error initializing Discord RPC: {ex.Message}");
            }
        }

        private string? lastSongName;
        private string? lastDifficulty;
        private bool isInLevel = false;
        private bool isPaused = false;
        private double remainingTime = 0;

        private void UpdatePresence(string jsonData)
        {
            UpdatePresence();
        }
        
        private void UpdatePresence()
        {
            bool currentlyInLevel = MapData.Instance.InLevel;

            if(currentlyInLevel && !isInLevel)
            {
                isInLevel = true;
                SongPresence();
            }
            else if (!currentlyInLevel && isInLevel)
            {
                isInLevel = false;
                MainMenuPresence("Main Menu");
            } 
            else if (MapData.Instance.LevelPaused && !isPaused)
            {
                PausePresence();
            }
            else if (!MapData.Instance.LevelPaused && isPaused)
            {
                UnpausePresence();
            }
        }

        private void PausePresence()
        {
            try 
            {
                if (IsClientInitialized())
                {
                    isPaused = true;
                    SaveRemainingTime();
                    Log!.Info("Game is Paused. Updating Presence...");
                    var currentSong = MapData.Instance;
                    if (currentSong != null)
                    {

                        client!.SetPresence(new RichPresence
                        {
                            Details = $"(Level Is Paused) {currentSong.SongName} by {currentSong.SongAuthor}",
                            State = $"{currentSong.Difficulty} - Mapped by {currentSong.Mapper}",
                            Assets = new Assets
                            {
                                LargeImageKey = $"{currentSong.CoverImage}",
                                LargeImageText = $"{currentSong.SongName}",
                            }
                        });

                        Log!.Info($"Paused presence updated: Details = (Level Is Paused) {currentSong.SongName}, State = {currentSong.Difficulty}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log!.Error($"Error in PausePresence: {ex.Message}");
            }
        }

        private void UnpausePresence()
        {
            try 
            {
                if (IsClientInitialized())
                {
                    Log!.Info("Game is unpaused. Updating Presence...");
                    isPaused = false;
                    var currentSong = MapData.Instance;
                    if (currentSong != null && currentSong.InLevel)
                    {
                        DateTime endTime = DateTime.UtcNow.AddSeconds(remainingTime);

                        client!.SetPresence(new RichPresence
                        {
                            Details = $"{currentSong.SongName} by {currentSong.SongAuthor}",
                            State = $"{currentSong.Difficulty} - Mapped by {currentSong.Mapper}",
                            Timestamps = new Timestamps
                            {
                                End = endTime
                            },
                            Assets = new Assets
                            {
                                LargeImageKey = $"{currentSong.CoverImage}",
                                LargeImageText = $"{currentSong.SongName}"
                            }
                        });

                        Log!.Info($"Presence Updated: Details = {currentSong.SongName} by {currentSong.SongAuthor}, State = {currentSong.Difficulty} - Mapped by {currentSong.Mapper}, EndTime = {endTime}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log!.Error($"Error in UnpausePresence: {ex.Message}");
            }
        }

        private void SaveRemainingTime()
        {
            var currentSong = MapData.Instance;
            if (currentSong != null)
            {
                double songDuration = currentSong.Duration;
                double currentTime = LiveData.Instance.TimeElapsed;
                remainingTime = songDuration - currentTime;
            }
        }

        private async void SongPresence()
        {
            if (IsClientInitialized())
            {
                var currentSong = MapData.Instance;
        
                if (currentSong != null && currentSong.InLevel)
                {
                    lastSongName = currentSong.SongName;
                    lastDifficulty = currentSong.Difficulty;

                    double songDuration = currentSong.Duration;
                    double currentTime = LiveData.Instance.TimeElapsed;
                    remainingTime = songDuration - currentTime;

                    DateTime endTime = DateTime.UtcNow.AddSeconds(remainingTime);

                    Log!.Info("Updating Presence...");

                    await Task.Delay(3000);

                    UpdateRichPresence(currentSong, endTime);
                }
                else
                {
                    Log!.Warn("No current song data available or song is not playing.");
                }
            }
            else
            {
                Log!.Warn("Discord RPC client is not initialized.");
            }
        }

        private void UpdateRichPresence(MapData currentSong, DateTime endTime)
        {
            Log!.Info($"Presence Updated: Details = {currentSong.SongName} by {currentSong.SongAuthor}, State = {currentSong.Difficulty} - Mapped by {currentSong.Mapper}, EndTime = {endTime}");
            client!.SetPresence(new RichPresence
            {
                Details = $"{currentSong.SongName} by {currentSong.SongAuthor}",
                State = $"{currentSong.Difficulty} - Mapped by {currentSong.Mapper}",
                Timestamps = new Timestamps
                {
                    End = endTime
                },
                Assets = new Assets
                {
                    LargeImageKey = currentSong.CoverImage,
                    LargeImageText = $"{currentSong.SongName}"
                }
            });
        }

        private void MainMenuPresence(string state)
        {
            if (IsClientInitialized())
            {
                client!.SetPresence(new RichPresence
                {
                    Details = "Playing Beat Saber",
                    State = state,
                    Timestamps = Timestamps.Now,
                    Assets = new Assets
                    {
                        LargeImageKey = "beat_saber",
                        LargeImageText = "Browsing the Menu"
                    }
                });

                Log!.Info($"Discord presence updated: {state}");
            }
            else
            {
                Log!.Warn("Discord RPC client is not initialized.");
            }
        }

        private bool IsClientInitialized()
        {
            if (client != null && client.IsInitialized)
            {
                return true;
            }
    
            Log!.Warn("Discord RPC client is not initialized.");
            return false;
        }

        [OnExit]
        private void OnApplicationQuit()
        {
            Log!.Info("Beat Saber Rich Presence Plugin Unloading...");
        
            if (client != null)
            {
                client.Dispose();
                Log.Info("Discord RPC client disposed.");
            }
            else
            {
                Log.Warn("Discord RPC client was null at exit.");
            }
        }
    }
}