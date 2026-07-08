using CommunityToolkit.Mvvm.ComponentModel;
using HandballManager.Models;
using HandballManager.Services;

namespace HandballManager.ViewModels;

public partial class MaskedPlayerProxy : ObservableObject
{
    private readonly Player _player;
    private readonly Func<bool> _isKnown;

    public MaskedPlayerProxy(Player player, Func<bool> isKnown)
    {
        _player = player;
        _isKnown = isKnown;
    }

    public void NotifyAllChanged() => OnPropertyChanged(string.Empty);

    private string Attr(string key, int actual) =>
        _isKnown() ? actual.ToString() : Estimation.Range(_player.Id, key, actual, 1, 20, 6);

    private string Overall(string key, int actual) =>
        _isKnown() ? actual.ToString() : Estimation.Range(_player.Id, key, actual, 10, 99, 12);

    public string Overall100 => Overall("Overall100", _player.Overall100);

    public string BuyoutFeeLabel
    {
        get
        {
            if (_isKnown())
                return $"{_player.BuyoutFee:N0} € buyout";

            // Keep in the women's market range.
            decimal est = _player.BuyoutFee;
            var range = Estimation.EuroRange(_player.Id, "BuyoutFee", est, 0, 250_000, 80_000);
            return $"{range} buyout";
        }
    }

    // Technical
    public string Dribbling => Attr(nameof(Dribbling), _player.Dribbling);
    public string Finishing => Attr(nameof(Finishing), _player.Finishing);
    public string LongThrows => Attr(nameof(LongThrows), _player.LongThrows);
    public string Marking => Attr(nameof(Marking), _player.Marking);
    public string SevenMeterTaking => Attr(nameof(SevenMeterTaking), _player.SevenMeterTaking);
    public string Tackling => Attr(nameof(Tackling), _player.Tackling);
    public string Technique => Attr(nameof(Technique), _player.Technique);
    public string Receiving => Attr(nameof(Receiving), _player.Receiving);
    public string Passing => Attr(nameof(Passing), _player.Passing);

    // Goalkeeping
    public string AerialReach => Attr(nameof(AerialReach), _player.AerialReach);
    public string Communication => Attr(nameof(Communication), _player.Communication);
    public string Eccentricity => Attr(nameof(Eccentricity), _player.Eccentricity);
    public string Handling => Attr(nameof(Handling), _player.Handling);
    public string Throwing => Attr(nameof(Throwing), _player.Throwing);
    public string OneOnOnes => Attr(nameof(OneOnOnes), _player.OneOnOnes);
    public string Reflexes => Attr(nameof(Reflexes), _player.Reflexes);

    // Mental
    public string Aggression => Attr(nameof(Aggression), _player.Aggression);
    public string Anticipation => Attr(nameof(Anticipation), _player.Anticipation);
    public string Composure => Attr(nameof(Composure), _player.Composure);
    public string Concentration => Attr(nameof(Concentration), _player.Concentration);
    public string Decisions => Attr(nameof(Decisions), _player.Decisions);
    public string Determination => Attr(nameof(Determination), _player.Determination);
    public string Flair => Attr(nameof(Flair), _player.Flair);
    public string Leadership => Attr(nameof(Leadership), _player.Leadership);
    public string OffTheBall => Attr(nameof(OffTheBall), _player.OffTheBall);
    public string Positioning => Attr(nameof(Positioning), _player.Positioning);
    public string Teamwork => Attr(nameof(Teamwork), _player.Teamwork);
    public string Vision => Attr(nameof(Vision), _player.Vision);

    // Physical
    public string Acceleration => Attr(nameof(Acceleration), _player.Acceleration);
    public string Agility => Attr(nameof(Agility), _player.Agility);
    public string Balance => Attr(nameof(Balance), _player.Balance);
    public string JumpingReach => Attr(nameof(JumpingReach), _player.JumpingReach);
    public string NaturalFitness => Attr(nameof(NaturalFitness), _player.NaturalFitness);
    public string Pace => Attr(nameof(Pace), _player.Pace);
    public string Stamina => Attr(nameof(Stamina), _player.Stamina);
    public string Strength => Attr(nameof(Strength), _player.Strength);
}

