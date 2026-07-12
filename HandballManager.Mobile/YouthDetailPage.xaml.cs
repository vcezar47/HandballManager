using HandballManager.Mobile.Infrastructure;
using HandballManager.ViewModels;

namespace HandballManager.Mobile;

public partial class YouthDetailPage : ContentPage
{
	public YouthDetailPage(YouthDetailViewModel vm)
	{
		InitializeComponent();
		BindingContext = vm;

		var technical = vm.IsGoalkeeper
			? new (string, int)[]
			{
				("Reflexes", vm.Reflexes), ("One on Ones", vm.OneOnOnes),
				("Handling", vm.Handling), ("Aerial Reach", vm.AerialReach),
				("Communication", vm.Communication), ("Eccentricity", vm.Eccentricity),
				("Throwing", vm.Throwing), ("Passing", vm.Passing), ("Receiving", vm.Receiving),
			}
			: new (string, int)[]
			{
				("Finishing", vm.Finishing), ("Technique", vm.Technique),
				("Dribbling", vm.Dribbling), ("Passing", vm.Passing),
				("Receiving", vm.Receiving), ("Marking", vm.Marking),
				("Tackling", vm.Tackling), ("Long Throws", vm.LongThrows),
				("7m Taking", vm.SevenMeterTaking),
			};

		TechnicalHost.Content = AttributeGridBuilder.Build(technical);

		MentalHost.Content = AttributeGridBuilder.Build(new (string, int)[]
		{
			("Aggression", vm.Aggression), ("Anticipation", vm.Anticipation),
			("Composure", vm.Composure), ("Concentration", vm.Concentration),
			("Decisions", vm.Decisions), ("Determination", vm.Determination),
			("Flair", vm.Flair), ("Leadership", vm.Leadership),
			("Off the Ball", vm.OffTheBall), ("Positioning", vm.Positioning),
			("Teamwork", vm.Teamwork), ("Vision", vm.Vision),
		});

		PhysicalHost.Content = AttributeGridBuilder.Build(new (string, int)[]
		{
			("Acceleration", vm.Acceleration), ("Agility", vm.Agility),
			("Balance", vm.Balance), ("Jumping Reach", vm.JumpingReach),
			("Natural Fitness", vm.NaturalFitness), ("Pace", vm.Pace),
			("Stamina", vm.Stamina), ("Strength", vm.Strength),
		});
	}
}
