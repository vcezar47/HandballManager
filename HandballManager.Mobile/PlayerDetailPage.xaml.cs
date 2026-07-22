using HandballManager.Mobile.Infrastructure;
using HandballManager.ViewModels;

namespace HandballManager.Mobile;

public partial class PlayerDetailPage : ContentPage
{
	public PlayerDetailPage(PlayerDetailViewModel vm)
	{
		InitializeComponent();
		BindingContext = vm;

		// Masked proxy: own/scouted players show exact values, others show estimation ranges.
		var m = vm.Masked;

		// Season progression is keyed by attribute property name, which is not always the
		// label we show ("7m Taking" is SevenMeterTaking), so each row carries both.
		var changes = vm.Player.SeasonAttributeChanges;
		View Build(params (string Label, string Key, string Value)[] rows)
			=> AttributeGridBuilder.Build(rows
				.Select(r => (r.Label, r.Value, changes.GetValueOrDefault(r.Key)))
				.ToList());

		TechnicalHost.Content = vm.Player.Position == "GK"
			? Build(
				("Reflexes", "Reflexes", m.Reflexes), ("One on Ones", "OneOnOnes", m.OneOnOnes),
				("Handling", "Handling", m.Handling), ("Aerial Reach", "AerialReach", m.AerialReach),
				("Communication", "Communication", m.Communication), ("Eccentricity", "Eccentricity", m.Eccentricity),
				("Throwing", "Throwing", m.Throwing), ("Passing", "Passing", m.Passing),
				("Receiving", "Receiving", m.Receiving))
			: Build(
				("Finishing", "Finishing", m.Finishing), ("Technique", "Technique", m.Technique),
				("Dribbling", "Dribbling", m.Dribbling), ("Passing", "Passing", m.Passing),
				("Receiving", "Receiving", m.Receiving), ("Marking", "Marking", m.Marking),
				("Tackling", "Tackling", m.Tackling), ("Long Throws", "LongThrows", m.LongThrows),
				("7m Taking", "SevenMeterTaking", m.SevenMeterTaking));

		MentalHost.Content = Build(
			("Aggression", "Aggression", m.Aggression), ("Anticipation", "Anticipation", m.Anticipation),
			("Composure", "Composure", m.Composure), ("Concentration", "Concentration", m.Concentration),
			("Decisions", "Decisions", m.Decisions), ("Determination", "Determination", m.Determination),
			("Flair", "Flair", m.Flair), ("Leadership", "Leadership", m.Leadership),
			("Off the Ball", "OffTheBall", m.OffTheBall), ("Positioning", "Positioning", m.Positioning),
			("Teamwork", "Teamwork", m.Teamwork), ("Vision", "Vision", m.Vision));

		PhysicalHost.Content = Build(
			("Acceleration", "Acceleration", m.Acceleration), ("Agility", "Agility", m.Agility),
			("Balance", "Balance", m.Balance), ("Jumping Reach", "JumpingReach", m.JumpingReach),
			("Natural Fitness", "NaturalFitness", m.NaturalFitness), ("Pace", "Pace", m.Pace),
			("Stamina", "Stamina", m.Stamina), ("Strength", "Strength", m.Strength));
	}
}
