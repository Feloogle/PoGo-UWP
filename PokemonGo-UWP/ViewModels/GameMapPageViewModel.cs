﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Geolocation;
using Windows.Devices.Sensors;
using Windows.Phone.Devices.Notification;
using Windows.System.Threading;
using Windows.UI.Popups;
using Windows.UI.Xaml.Navigation;
using PokemonGo.RocketAPI;
using PokemonGo_UWP.Entities;
using PokemonGo_UWP.Utils;
using PokemonGo_UWP.Views;
using POGOProtos.Data;
using POGOProtos.Data.Player;
using POGOProtos.Inventory;
using POGOProtos.Map.Pokemon;
using Template10.Common;
using Template10.Mvvm;
using Template10.Services.NavigationService;
using Universal_Authenticator_v2.Views;

namespace PokemonGo_UWP.ViewModels
{
    public class GameMapPageViewModel : ViewModelBase
    {

        #region Lifecycle Handlers

        /// <summary>
        /// 
        /// </summary>
        /// <param name="parameter"></param>
        /// <param name="mode"></param>
        /// <param name="suspensionState"></param>
        /// <returns></returns>
        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> suspensionState)
        {
            // Prevent from going back to other pages
            NavigationService.ClearHistory();
            if (parameter is bool)
            {
                // First time navigating here, we need to initialize data updating but only if we have GPS access
                await Dispatcher.DispatchAsync(async () => { 
                    var accessStatus = await Geolocator.RequestAccessAsync();
                    switch (accessStatus)
                    {
                        case GeolocationAccessStatus.Allowed:
                            await GameClient.InitializeDataUpdate();
                            break;
                        default:
                            Logger.Write("Error during GPS activation");
                            await new MessageDialog("We need GPS permissions to run the game, please enable it and try again.").ShowAsyncQueue();
                            BootStrapper.Current.Exit();
                            break;
                    }
                });
            }
            if (suspensionState.Any())
            {
                // Recovering the state                
                PlayerProfile = (PlayerData) suspensionState[nameof(PlayerProfile)];
                PlayerStats = (PlayerStats) suspensionState[nameof(PlayerStats)];                
            }
            else
            {
                // No saved state, get them from the client                
                PlayerProfile = (await GameClient.GetProfile()).PlayerData;
                InventoryDelta = (await GameClient.GetInventory()).InventoryDelta;
                var tmpStats = InventoryDelta.InventoryItems.First(item => item.InventoryItemData.PlayerStats != null).InventoryItemData.PlayerStats;
                if (PlayerStats != null && PlayerStats.Level != tmpStats.Level)
                {
                    // TODO: report level increase
                }
                PlayerStats = tmpStats;
            }
            await Task.CompletedTask;
        }

        /// <summary>
        /// Save state before navigating
        /// </summary>
        /// <param name="suspensionState"></param>
        /// <param name="suspending"></param>
        /// <returns></returns>
        public override async Task OnNavigatedFromAsync(IDictionary<string, object> suspensionState, bool suspending)
        {
            if (suspending)
            {
                suspensionState[nameof(PlayerProfile)] = PlayerProfile;
                suspensionState[nameof(PlayerStats)] = PlayerStats;
            }
            await Task.CompletedTask;
        }

        public override async Task OnNavigatingFromAsync(NavigatingEventArgs args)
        {
            args.Cancel = false;
            await Task.CompletedTask;
        }

        #endregion

        #region Game Management Vars

        /// <summary>
        ///     We use it to notify that we found at least one catchable Pokemon in our area
        /// </summary>
        private readonly VibrationDevice _vibrationDevice;

        /// <summary>
        ///     True if the phone can vibrate (e.g. the app is not in background)
        /// </summary>
        public bool CanVibrate;

        /// <summary>
        ///     Player's profile, we use it just for the username
        /// </summary>
        private PlayerData _playerProfile;

        /// <summary>
        ///     Stats for the current player, including current level and experience related stuff
        /// </summary>
        private PlayerStats _playerStats;

        /// <summary>
        ///     Player's inventory
        ///     TODO: do we really need it?
        /// </summary>
        private InventoryDelta _inventoryDelta;

        #endregion

        #region Bindable Game Vars   

        public string CurrentVersion => GameClient.CurrentVersion;

        /// <summary>
        ///     Key for Bing's Map Service (not included in GIT, you need to get your own token to use maps!)
        /// </summary>
        public string MapServiceToken => ApplicationKeys.MapServiceToken;

        /// <summary>
        ///     Player's profile, we use it just for the username
        /// </summary>
        public PlayerData PlayerProfile
        {
            get { return _playerProfile; }
            set { Set(ref _playerProfile, value); }
        }

        /// <summary>
        ///     Stats for the current player, including current level and experience related stuff
        /// </summary>
        public PlayerStats PlayerStats
        {
            get { return _playerStats; }
            set { Set(ref _playerStats, value); }
        }

        public InventoryDelta InventoryDelta
        {
            get { return _inventoryDelta; }
            set { Set(ref _inventoryDelta, value); }
        }

        /// <summary>
        ///     Collection of Pokemon in 1 step from current position
        /// </summary>
        public static ObservableCollection<MapPokemonWrapper> CatchablePokemons => GameClient.CatchablePokemons;

        /// <summary>
        ///     Collection of Pokemon in 2 steps from current position
        /// </summary>
        public static ObservableCollection<NearbyPokemon> NearbyPokemons => GameClient.NearbyPokemons;

        /// <summary>
        ///     Collection of Pokestops in the current area
        /// </summary>
        public static ObservableCollection<FortDataWrapper> NearbyPokestops => GameClient.NearbyPokestops;

        #endregion

        #region Game Logic

        #region Logout

        private DelegateCommand _doPtcLogoutCommand;

        public DelegateCommand DoPtcLogoutCommand => _doPtcLogoutCommand ?? (
            _doPtcLogoutCommand = new DelegateCommand(() =>
            {
                // Clear stored token
                GameClient.DoLogout();
                // Navigate to login page
                NavigationService.Navigate(typeof(MainPage));
            }, () => true)
            );


        #endregion       

        #endregion

    }
}
