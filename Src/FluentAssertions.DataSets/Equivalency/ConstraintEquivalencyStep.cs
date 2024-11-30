﻿using System.Collections.Generic;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using FluentAssertions.DataSets.Common;
using FluentAssertions.Equivalency;
using FluentAssertions.Execution;

namespace FluentAssertions.DataSets.Equivalency;

public class ConstraintEquivalencyStep : EquivalencyStep<Constraint>
{
    protected override EquivalencyResult OnHandle(Comparands comparands, IEquivalencyValidationContext context,
        IValidateChildNodeEquivalency nestedValidator)
    {
        var assertionChain = AssertionChain.GetOrCreate().For(context);

        if (comparands.Subject is not Constraint)
        {
            assertionChain
                .FailWith("Expected {context:constraint} to be a value of type Constraint, but found {0}",
                    comparands.Subject.GetType());
        }
        else
        {
            var subject = (Constraint)comparands.Subject;
            var expectation = (Constraint)comparands.Expectation;

            var selectedMembers = GetMembersFromExpectation(comparands, context.CurrentNode, context.Options)
                .ToDictionary(member => member.Name);

            CompareCommonProperties(context, nestedValidator, context.Options, subject, expectation, selectedMembers, assertionChain);

            bool matchingType = subject.GetType() == expectation.GetType();

            assertionChain
                .ForCondition(matchingType)
                .FailWith("Expected {context:constraint} to be of type {0}, but found {1}", expectation.GetType(),
                    subject.GetType());

            if (matchingType)
            {
                if (subject is UniqueConstraint subjectUniqueConstraint
                    && expectation is UniqueConstraint expectationUniqueConstraint)
                {
                    CompareConstraints(nestedValidator, context, subjectUniqueConstraint, expectationUniqueConstraint,
                        selectedMembers, assertionChain);
                }
                else if (subject is ForeignKeyConstraint subjectForeignKeyConstraint
                         && expectation is ForeignKeyConstraint expectationForeignKeyConstraint)
                {
                    CompareConstraints(nestedValidator, context, subjectForeignKeyConstraint, expectationForeignKeyConstraint,
                        selectedMembers, assertionChain);
                }
                else
                {
                    assertionChain
                        .FailWith("Don't know how to handle {constraint:a Constraint} of type {0}", subject.GetType());
                }
            }
        }

        return EquivalencyResult.EquivalencyProven;
    }

    private static void CompareCommonProperties(IEquivalencyValidationContext context, IValidateChildNodeEquivalency parent,
        IEquivalencyOptions options, Constraint subject, Constraint expectation,
        Dictionary<string, IMember> selectedMembers, AssertionChain assertionChain)
    {
        if (selectedMembers.ContainsKey("ConstraintName"))
        {
            assertionChain
                .ForCondition(subject.ConstraintName == expectation.ConstraintName)
                .FailWith("Expected {context:constraint} to have a ConstraintName of {0}{reason}, but found {1}",
                    expectation.ConstraintName, subject.ConstraintName);
        }

        if (selectedMembers.ContainsKey("Table"))
        {
            assertionChain
                .ForCondition(subject.Table.TableName == expectation.Table.TableName)
                .FailWith(
                    "Expected {context:constraint} to be associated with a Table with TableName of {0}{reason}, but found {1}",
                    expectation.Table.TableName, subject.Table.TableName);
        }

        if (selectedMembers.TryGetValue("ExtendedProperties", out IMember expectationMember))
        {
            IMember matchingMember = FindMatchFor(expectationMember, context.CurrentNode, subject, options, assertionChain);

            if (matchingMember is not null)
            {
                var nestedComparands = new Comparands
                {
                    Subject = matchingMember.GetValue(subject),
                    Expectation = expectationMember.GetValue(expectation),
                    CompileTimeType = expectationMember.Type
                };

                parent.AssertEquivalencyOf(nestedComparands, context.AsNestedMember(expectationMember));
            }
        }
    }

    private static void CompareConstraints(IValidateChildNodeEquivalency parent, IEquivalencyValidationContext context,
        UniqueConstraint subject, UniqueConstraint expectation, Dictionary<string, IMember> selectedMembers, AssertionChain assertionChain)
    {
        assertionChain
            .ForCondition(subject.ConstraintName == expectation.ConstraintName)
            .FailWith("Expected {context:constraint} to be named {0}{reason}, but found {1}", expectation.ConstraintName,
                subject.ConstraintName);

        var nestedMember = MemberFactory.Create(
            typeof(Constraint).GetProperty(nameof(subject.ExtendedProperties)),
            context.CurrentNode);

        var nestedComparands = new Comparands
        {
            Subject = nestedMember.GetValue(subject),
            Expectation = nestedMember.GetValue(expectation),
            CompileTimeType = nestedMember.Type
        };

        parent.AssertEquivalencyOf(nestedComparands, context.AsNestedMember(nestedMember));

        if (selectedMembers.ContainsKey(nameof(expectation.IsPrimaryKey)))
        {
            assertionChain
                .ForCondition(subject.IsPrimaryKey == expectation.IsPrimaryKey)
                .FailWith("Expected {context:constraint} to be a {0} constraint{reason}, but found a {1} constraint",
                    expectation.IsPrimaryKey ? "Primary Key" : "Foreign Key",
                    subject.IsPrimaryKey ? "Primary Key" : "Foreign Key");
        }

        if (selectedMembers.ContainsKey(nameof(expectation.Columns)))
        {
            CompareConstraintColumns(subject.Columns, expectation.Columns, assertionChain);
        }
    }

    [SuppressMessage("Design", "MA0051:Method is too long", Justification = "Needs to be refactored")]
    private static void CompareConstraints(IValidateChildNodeEquivalency parent, IEquivalencyValidationContext context,
        ForeignKeyConstraint subject, ForeignKeyConstraint expectation, Dictionary<string, IMember> selectedMembers, AssertionChain assertionChain)
    {
        assertionChain
            .ForCondition(subject.ConstraintName == expectation.ConstraintName)
            .FailWith("Expected {context:constraint} to be named {0}{reason}, but found {1}", expectation.ConstraintName,
                subject.ConstraintName);

        var nestedMember = MemberFactory.Create(
            typeof(Constraint).GetProperty(nameof(subject.ExtendedProperties)),
            context.CurrentNode);

        var nestedComparands = new Comparands
        {
            Subject = nestedMember.GetValue(subject),
            Expectation = nestedMember.GetValue(expectation),
            CompileTimeType = nestedMember.Type
        };

        parent.AssertEquivalencyOf(nestedComparands, context.AsNestedMember(nestedMember));

        if (selectedMembers.ContainsKey(nameof(expectation.RelatedTable)))
        {
            assertionChain
                .ForCondition(subject.RelatedTable.TableName == expectation.RelatedTable.TableName)
                .FailWith("Expected {context:constraint} to have a related table named {0}{reason}, but found {1}",
                    expectation.RelatedTable.TableName, subject.RelatedTable.TableName);
        }

        if (selectedMembers.ContainsKey(nameof(expectation.AcceptRejectRule)))
        {
            assertionChain
                .ForCondition(subject.AcceptRejectRule == expectation.AcceptRejectRule)
                .FailWith(
                    "Expected {context:constraint} to have AcceptRejectRule.{0}{reason}, but found AcceptRejectRule.{1}",
                    expectation.AcceptRejectRule, subject.AcceptRejectRule);
        }

        if (selectedMembers.ContainsKey(nameof(expectation.DeleteRule)))
        {
            assertionChain
                .ForCondition(subject.DeleteRule == expectation.DeleteRule)
                .FailWith("Expected {context:constraint} to have DeleteRule Rule.{0}{reason}, but found Rule.{1}",
                    expectation.DeleteRule, subject.DeleteRule);
        }

        if (selectedMembers.ContainsKey(nameof(expectation.UpdateRule)))
        {
            assertionChain
                .ForCondition(subject.UpdateRule == expectation.UpdateRule)
                .FailWith("Expected {context:constraint} to have UpdateRule Rule.{0}{reason}, but found Rule.{1}",
                    expectation.UpdateRule, subject.UpdateRule);
        }

        if (selectedMembers.ContainsKey(nameof(expectation.Columns)))
        {
            CompareConstraintColumns(subject.Columns, expectation.Columns, assertionChain);
        }

        if (selectedMembers.ContainsKey(nameof(expectation.RelatedColumns)))
        {
            CompareConstraintColumns(subject.RelatedColumns, expectation.RelatedColumns, assertionChain);
        }
    }

    private static void CompareConstraintColumns(DataColumn[] subjectColumns, DataColumn[] expectationColumns, AssertionChain assertionChain)
    {
        var subjectColumnNames = new HashSet<string>(subjectColumns.Select(col => col.ColumnName));
        var expectationColumnNames = new HashSet<string>(expectationColumns.Select(col => col.ColumnName));

        var missingColumnNames = expectationColumnNames.Except(subjectColumnNames).ToList();
        var extraColumnNames = subjectColumnNames.Except(expectationColumnNames).ToList();

        var failureMessage = new StringBuilder();

        if (missingColumnNames.Count > 0)
        {
            failureMessage.Append("Expected {context:constraint} to include ");

            if (missingColumnNames.Count == 1)
            {
                failureMessage.Append("column ").Append(missingColumnNames.Single());
            }
            else
            {
                failureMessage.Append("columns ").Append(missingColumnNames.JoinUsingWritingStyle());
            }

            failureMessage
                .Append("{reason}, but constraint does not include ")
                .Append(missingColumnNames.Count == 1
                    ? "that column. "
                    : "these columns. ");
        }

        if (extraColumnNames.Count > 0)
        {
            failureMessage.Append("Did not expect {context:constraint} to include ");

            if (extraColumnNames.Count == 1)
            {
                failureMessage.Append("column ").Append(extraColumnNames.Single());
            }
            else
            {
                failureMessage.Append("columns ").Append(extraColumnNames.JoinUsingWritingStyle());
            }

            failureMessage.Append("{reason}, but it does.");
        }

        bool successful = failureMessage.Length == 0;

        assertionChain
            .ForCondition(successful)
            .FailWith(failureMessage.ToString());
    }

    private static IMember FindMatchFor(IMember selectedMemberInfo, INode currentNode, object subject,
        IEquivalencyOptions config, AssertionChain assertionChain)
    {
        IEnumerable<IMember> query =
            from rule in config.MatchingRules
            let match = rule.Match(selectedMemberInfo, subject, currentNode, config, assertionChain)
            where match is not null
            select match;

        return query.FirstOrDefault();
    }

    private static IEnumerable<IMember> GetMembersFromExpectation(Comparands comparands, INode currentNode,
        IEquivalencyOptions options)
    {
        IEnumerable<IMember> members = Enumerable.Empty<IMember>();

        foreach (IMemberSelectionRule rule in options.SelectionRules)
        {
            // Within a ConstraintCollection, different types of Constraint are kept polymorphically.
            // As such, the concept of "compile-time type" isn't meaningful, and we override this
            // with the discovered type of the constraint at runtime.
            members = rule.SelectMembers(currentNode, members,
                new MemberSelectionContext(comparands.RuntimeType, comparands.RuntimeType, options));
        }

        return members;
    }
}
