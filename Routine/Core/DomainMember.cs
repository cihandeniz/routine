using System.Collections.Generic;
using System.Linq;
using Routine.Core.Service;

namespace Routine.Core
{
	public class DomainMember
	{
		private readonly ICoreContext ctx;

		public DomainMember(ICoreContext ctx)
		{
			this.ctx = ctx;
		}

		private DomainType domainType;
		private IMember member;

		public string Id { get; private set; }
		public Marks Marks { get; private set; }
		public bool IsList{get;private set;}
		public string ViewModelId{get;private set;}
		public bool IsHeavy{get;private set;}

		public DomainMember For(DomainType domainType, IMember member)
		{
			this.domainType = domainType;
			this.member = member;

			Id = member.Name;
			Marks = new Marks(ctx.CodingStyle.MemberMarkSelector.Select(member));
			IsList = member.ReturnType.CanBeCollection();
			ViewModelId = ctx.CodingStyle.ModelIdSerializer.Serialize(IsList ?member.ReturnType.GetItemType():member.ReturnType);
			IsHeavy = ctx.CodingStyle.MemberIsHeavyExtractor.Extract(member);

			return this;
		}

		public bool MarkedAs(string mark)
		{
			return Marks.Has(mark);
		}

		public MemberModel GetModel()
		{
			return new MemberModel {
				Id = Id,
				Marks = Marks.List,
				IsHeavy = IsHeavy,
				IsList = IsList,
				ViewModelId = ViewModelId
			};
		}

		public MemberData CreateData(object target)
		{
			var result = new MemberData();

			result.ModelId = Id;
			result.Value = ctx.CreateValueData(member.FetchFrom(target), IsList, ViewModelId);

			return result;
		}
	}
}
