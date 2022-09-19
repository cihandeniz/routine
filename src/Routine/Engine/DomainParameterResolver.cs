﻿using Routine.Core;

namespace Routine.Engine;

internal class DomainParameterResolver<T> where T : class, IParametric
{
    private readonly List<DomainParameter.Group<T>> groups;
    private readonly Dictionary<string, ParameterValueData> parameterValueDatas;

    public DomainParameterResolver(List<DomainParameter.Group<T>> groups, Dictionary<string, ParameterValueData> parameterValueDatas)
    {
        parameterValueDatas ??= new Dictionary<string, ParameterValueData>();

        this.groups = groups;
        this.parameterValueDatas = parameterValueDatas;
    }

    public async Task<Resolution> ResolveAsync()
    {
        var mostCompatibleGroup = FindMostCompatibleGroup();
        var parameters = await PrepareParametersForAsync(mostCompatibleGroup);

        return new Resolution(mostCompatibleGroup.Parametric, parameters);
    }

    private async Task<object[]> PrepareParametersForAsync(DomainParameter.Group<T> group)
    {
        var result = new object[group.Parameters.Count];

        foreach (var parameter in group.Parametric.Parameters)
        {
            if (parameterValueDatas.TryGetValue(parameter.Name, out var parameterValueData))
            {
                result[parameter.Index] = await group.Parameters[parameter.Index].LocateAsync(parameterValueData);
            }
            else if (parameter.IsOptional && parameter.HasDefaultValue)
            {
                result[parameter.Index] = parameter.DefaultValue;
            }
        }

        return result;
    }

    private DomainParameter.Group<T> FindMostCompatibleGroup()
    {
        if (groups.Count == 1)
        {
            return groups[0];
        }

        var exactMatch = groups.SingleOrDefault(MatchesExactlyWithValues);

        if (exactMatch != null)
        {
            return exactMatch;
        }

        var foundGroups = FindGroupsWithMostMatchedParameters();

        return foundGroups.Count == 1
            ? foundGroups[0]
            : GetFirstGroupWithLeastNonMatchedParameters(foundGroups);
    }

    private bool MatchesExactlyWithValues(DomainParameter.Group<T> group) =>
        group.Parametric.Parameters.Count == parameterValueDatas.Count &&
        group.Parametric.Parameters.All(p => parameterValueDatas.ContainsKey(p.Name));

    private List<DomainParameter.Group<T>> FindGroupsWithMostMatchedParameters()
    {
        var result = new List<DomainParameter.Group<T>>();

        var matchCount = int.MinValue;

        foreach (var group in groups.OrderByDescending(o => o.Parameters.Count))
        {
            var tempCount = group.Parametric.Parameters.Count(p => parameterValueDatas.ContainsKey(p.Name));

            if (tempCount > matchCount)
            {
                result.Clear();
                result.Add(group);
                matchCount = tempCount;
            }
            else if (tempCount == matchCount)
            {
                result.Add(group);
            }
        }

        return result;
    }

    private DomainParameter.Group<T> GetFirstGroupWithLeastNonMatchedParameters(List<DomainParameter.Group<T>> foundGroups)
    {
        DomainParameter.Group<T> result = null;

        var nonMatchCount = int.MaxValue;

        foreach (var group in foundGroups.OrderByDescending(o => o.Parameters.Count))
        {
            var tempCount = group.Parametric.Parameters.Count(p => !parameterValueDatas.ContainsKey(p.Name));

            if (tempCount < nonMatchCount)
            {
                result = group;
                nonMatchCount = tempCount;
            }
        }

        return result;
    }

    public record Resolution(T Result, object[] Parameters);
}
