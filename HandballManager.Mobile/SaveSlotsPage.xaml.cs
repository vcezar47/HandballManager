using System.Windows.Input;
using HandballManager.Mobile.Session;

namespace HandballManager.Mobile;

public partial class SaveSlotsPage : ContentPage
{
	public enum Mode { New, Load }

	private readonly Mode _mode;
	private bool _busy;

	public ICommand SlotTappedCommand { get; }
	public ICommand DeleteSlotCommand { get; }

	public SaveSlotsPage(Mode mode)
	{
		InitializeComponent();
		_mode = mode;
		SlotTappedCommand = new Command<SlotRow>(async r => await OnSlotTapped(r));
		DeleteSlotCommand = new Command<SlotRow>(async r => await OnDeleteSlot(r));
		BindingContext = this;

		HeaderLabel.Text = mode == Mode.New ? "NEW GAME" : "LOAD GAME";
		SubLabel.Text = mode == Mode.New ? "Choose a slot" : "Load a career";
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await RefreshAsync();
	}

	private async Task RefreshAsync()
	{
		var infos = await SaveSlots.ReadAllAsync();
		var rows = infos.Select(i => new SlotRow(i, _mode)).ToList();
		BindableLayout.SetItemsSource(SlotsHost, rows);
	}

	private async Task OnSlotTapped(SlotRow? row)
	{
		if (row == null || _busy) return;
		var info = row.Info;

		if (_mode == Mode.New)
		{
			if (info.Occupied)
			{
				bool replace = await DisplayAlertAsync("Overwrite Slot",
					$"Slot {info.Slot} holds {info.ClubName}. Start a new career here and erase it?", "Overwrite", "Cancel");
				if (!replace) return;
			}

			SetBusy(true, "Building the world…");
			try
			{
				await Task.Run(() => GameSession.NewGameAsync(info.Slot));
				await Shell.Current.GoToAsync("//setup");
			}
			catch (Exception ex) { await DisplayAlertAsync("New Game Failed", ex.Message, "OK"); }
			finally { SetBusy(false); }
		}
		else // Load
		{
			if (!info.Occupied) return;

			SetBusy(true, "Loading your career…");
			try
			{
				var session = await Task.Run(() => GameSession.LoadSlotAsync(info.Slot));
				if (session == null)
				{
					await DisplayAlertAsync("Load Game", "This slot could not be loaded.", "OK");
					await RefreshAsync();
					return;
				}
				await Shell.Current.GoToAsync("//game/home");
			}
			catch (Exception ex) { await DisplayAlertAsync("Load Failed", ex.Message, "OK"); }
			finally { SetBusy(false); }
		}
	}

	private async Task OnDeleteSlot(SlotRow? row)
	{
		if (row == null || !row.Info.Occupied || _busy) return;

		bool del = await DisplayAlertAsync("Delete Career",
			$"Permanently delete {row.Info.ClubName} in slot {row.Info.Slot}?", "Delete", "Cancel");
		if (!del) return;

		SaveSlots.Delete(row.Info.Slot);
		await RefreshAsync();
	}

	private async void OnBackClicked(object? sender, EventArgs e) => await Navigation.PopAsync();

	private void SetBusy(bool busy, string status = "")
	{
		_busy = busy;
		BusyOverlay.IsVisible = busy;
		BusySpinner.IsRunning = busy;
		BusyLabel.Text = status;
	}
}

public sealed class SlotRow
{
	public SlotInfo Info { get; }
	public bool IsEmpty => !Info.Occupied;
	public string EmptyHint { get; }

	public SlotRow(SlotInfo info, SaveSlotsPage.Mode mode)
	{
		Info = info;
		EmptyHint = mode == SaveSlotsPage.Mode.New ? "Tap to start a new career" : "No career here";
	}
}
