using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Autodesk.Revit.DB;
using SharedRevit.Utils;

namespace SharedRevit.Forms
{
    public partial class FilterCreateForm : System.Windows.Forms.Form
    {
        RevitUtilsDefault revitUtils = RevitUtilService.Get();
        CategoryNameMap categoryNameMap;
        Element selected;
        List<MyFilterRuleWrapper> filterRules = new List<MyFilterRuleWrapper>();
        public FilterCreateForm(Element elem)
        {
            InitializeComponent();
            CenterToParent();
            selected = elem;
            categoryNameMap = revitUtils.GetCategoryMap();
            categoryCombo.SelectedIndexChanged += CategoryCombo_SelectedIndexChanged;
            parameterCombo.SelectedIndexChanged += ParameterCombo_SelectedIndexChanged;
            Add.Click += Add_Click;
            remove.Click += Remove_Click;
            save.Click += Save_Click;
            cancel.Click += Cancel_Click;
            saveHidden.Click += SaveHidden_Click;

            revitUtils.GetCategories()
                .ForEach(c => categoryCombo.Items.Add(c.Name));

            categoryCombo.Text = elem.Category?.Name;
            Family family = null;
            if (elem is FamilyInstance familyInstance)
            {
                ParameterValueProvider provider = new ParameterValueProvider(
                    new ElementId(BuiltInParameter.SYMBOL_FAMILY_NAME_PARAM));

                // Create the rule using the static method
                FilterRule rule = new FilterStringRule(
                    provider,
                    new FilterStringEquals(),
                    familyInstance.Symbol.Family.Name);
                filterRules.Add(new MyFilterRuleWrapper(rule,"Family" , "FilterStringEquals", familyInstance.Symbol.Family.Name));

                if (!rule.ElementPasses(elem))
                {
                    MessageBox.Show("The element does not pass the filter rule. So this is broken");
                }
            }
            updateGrid();
        }

        private void SaveHidden_Click(object sender, EventArgs e)
        {
            string name = nameTextBox.Text.Trim();
            string selectedCategory = categoryCombo.SelectedItem as string;
            Category category = categoryNameMap.get_Item(selectedCategory);
            if (string.IsNullOrEmpty(name))
            {
                name = "New Filter";
            }
            revitUtils.CreateParameterFilter(name, filterRules, new[] { category.Id }, false);
            this.Close();
        }

        private void Cancel_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void Save_Click(object sender, EventArgs e)
        {
            string name = nameTextBox.Text.Trim();
            string selectedCategory = categoryCombo.SelectedItem as string;
            Category category = categoryNameMap.get_Item(selectedCategory);

            revitUtils.CreateParameterFilter(name, filterRules, new[] { category.Id }, true);
            this.Close();
        }

        private void Remove_Click(object sender, EventArgs e)
        {
            dataGrid.Rows.RemoveAt(dataGrid.CurrentCell.RowIndex);
        }

        private void Add_Click(object sender, EventArgs e)
        {
            string param = parameterCombo.SelectedItem?.ToString();
            string opp = opperationCombo.SelectedItem?.ToString();
            string val = valueTextBox.Text;

            if (string.IsNullOrEmpty(param) || string.IsNullOrEmpty(opp) || string.IsNullOrEmpty(val))
            {
                MessageBox.Show("Please select a parameter, operator, and value.");
                return;
            }

            Parameter p = selected.LookupParameter(param);
            if (p == null)
            {
                MessageBox.Show($"Parameter '{param}' not found in the selected element.");
                return;
            }

            ElementId provider = p.Id;
            FilterRule rule = null;

            try
            {
                switch (p.StorageType)
                {
                    case StorageType.String:
                        rule = CreateStringRule(provider, opp, val);
                        break;

                    case StorageType.Double:
                        if (double.TryParse(val, out double doubleVal))
                            rule = CreateDoubleRule(provider, opp, doubleVal);
                        else
                            throw new FormatException("Invalid double value.");
                        break;

                    case StorageType.Integer:
                        if (int.TryParse(val, out int intVal))
                            rule = CreateIntegerRule(provider, opp, intVal);
                        else
                            throw new FormatException("Invalid integer value.");
                        break;

                    case StorageType.ElementId:
                        rule = CreateElementIdRule(provider, opp, revitUtils.ConvertStringToElementId(p.AsValueString()));
                        break;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error creating rule: {ex.Message}");
                return;
            }

            if (rule != null)
            {
                filterRules.Add(new MyFilterRuleWrapper(rule, param, opp, val));
                updateGrid();
            }
        }
        private FilterRule CreateStringRule(ElementId provider, string op, string val)
        {
            return op switch
            {
                "FilterStringEquals" => ParameterFilterRuleFactory.CreateEqualsRule(provider, val, false),
                "FilterStringContains" => ParameterFilterRuleFactory.CreateContainsRule(provider, val, false),
                "FilterStringBeginsWith" => ParameterFilterRuleFactory.CreateBeginsWithRule(provider, val, false),
                "FilterStringEndsWith" => ParameterFilterRuleFactory.CreateEndsWithRule(provider, val, false),
                "FilterStringGreater" => ParameterFilterRuleFactory.CreateGreaterRule(provider, val, false),
                "FilterStringLess" => ParameterFilterRuleFactory.CreateLessRule(provider, val, false),
                _ => throw new ArgumentException($"Unsupported string operator: {op}")
            };
        }

        private FilterRule CreateDoubleRule(ElementId provider, string op, double val)
        {
            double tolerance = 0.0001;
            return op switch
            {
                "FilterNumericEquals" => ParameterFilterRuleFactory.CreateEqualsRule(provider, val, tolerance),
                "FilterNumericNotEquals" => ParameterFilterRuleFactory.CreateNotEqualsRule(provider, val, tolerance),
                "FilterNumericGreater" => ParameterFilterRuleFactory.CreateGreaterRule(provider, val, tolerance),
                "FilterNumericGreaterOrEqual" => ParameterFilterRuleFactory.CreateGreaterOrEqualRule(provider, val, tolerance),
                "FilterNumericLess" => ParameterFilterRuleFactory.CreateLessRule(provider, val, tolerance),
                "FilterNumericLessOrEqual" => ParameterFilterRuleFactory.CreateLessOrEqualRule(provider, val, tolerance),
                _ => throw new ArgumentException($"Unsupported numeric operator: {op}")
            };
        }

        private FilterRule CreateIntegerRule(ElementId provider, string op, int val)
        {
            return op switch
            {
                "FilterNumericEquals" => ParameterFilterRuleFactory.CreateEqualsRule(provider, val),
                "FilterNumericNotEquals" => ParameterFilterRuleFactory.CreateNotEqualsRule(provider, val),
                "FilterNumericGreater" => ParameterFilterRuleFactory.CreateGreaterRule(provider, val),
                "FilterNumericGreaterOrEqual" => ParameterFilterRuleFactory.CreateGreaterOrEqualRule(provider, val),
                "FilterNumericLess" => ParameterFilterRuleFactory.CreateLessRule(provider, val),
                "FilterNumericLessOrEqual" => ParameterFilterRuleFactory.CreateLessOrEqualRule(provider, val),
                _ => throw new ArgumentException($"Unsupported integer operator: {op}")
            };
        }

        private FilterRule CreateElementIdRule(ElementId provider, string op, ElementId val)
        {
            return op switch
            {
                "FilterElementIdEquals" => ParameterFilterRuleFactory.CreateEqualsRule(provider, val),
                "FilterElementIdNotEquals" => ParameterFilterRuleFactory.CreateNotEqualsRule(provider, val),
                _ => throw new ArgumentException($"Unsupported ElementId operator: {op}")
            };
        }


        private void ParameterCombo_SelectedIndexChanged(object sender, EventArgs e)
        {
            string selectedParameter = parameterCombo.Text;
            if (!string.IsNullOrEmpty(selectedParameter))
            {
                Parameter p = selected.LookupParameter(selectedParameter);
                string val = p.AsValueString();
                valueTextBox.Text = val ?? string.Empty;

                GetValidFilterRuleTypes(p).ForEach(ruleType =>
                {
                    opperationCombo.Items.Add(ruleType);
                });
            }
        }

        private void CategoryCombo_SelectedIndexChanged(object sender, EventArgs e)
        {
            string selectedCategory = categoryCombo.SelectedItem as string;
            Category category = categoryNameMap.get_Item(selectedCategory);
            ParameterSet parameters = selected.Parameters;

            parameterCombo.Items.Clear();
            foreach (Parameter param in parameters)
            {
                parameterCombo.Items.Add(param.Definition.Name);
            }
            parameterCombo.SelectedIndex = 0; // Select the first parameter by default
        }


        public static List<string> GetValidFilterRuleTypes(Parameter param)
        {
            List<string> ruleTypes = new List<string>();

            if (param == null)
                return ruleTypes;

            switch (param.StorageType)
            {
                case StorageType.Double:
                    ruleTypes.AddRange(new[]
                    {
                    "FilterNumericEquals",
                    "FilterNumericNotEquals",
                    "FilterNumericGreater",
                    "FilterNumericGreaterOrEqual",
                    "FilterNumericLess",
                    "FilterNumericLessOrEqual"
                });
                    break;

                case StorageType.Integer:
                    ruleTypes.AddRange(new[]
                    {
                    "FilterNumericEquals",
                    "FilterNumericNotEquals",
                    "FilterNumericGreater",
                    "FilterNumericGreaterOrEqual",
                    "FilterNumericLess",
                    "FilterNumericLessOrEqual"
                });
                    break;

                case StorageType.String:
                    ruleTypes.AddRange(new[]
                    {
                    "FilterStringEquals",
                    "FilterStringContains",
                    "FilterStringBeginsWith",
                    "FilterStringEndsWith",
                    "FilterStringGreater",
                    "FilterStringLess"
                });
                    break;

                case StorageType.ElementId:
                    ruleTypes.AddRange(new[]
                    {
                    "FilterElementIdEquals",
                    "FilterElementIdNotEquals"
                });
                    break;
            }

            return ruleTypes;
        }

        public void updateGrid()
        {
            dataGrid.Rows.Clear();
            foreach (var ruleWrapper in filterRules)
            {
                dataGrid.Rows.Add(
                    ruleWrapper.ParameterName,
                    ruleWrapper.Operator,
                    ruleWrapper.Value);
            }
        }
    }


    public static class FilterEvaluatorFactory
    {
        public static object GetEvaluator(string evaluatorName)
        {
            return evaluatorName switch
            {
                "FilterNumericEquals" => new FilterNumericEquals(),
                "FilterNumericGreater" => new FilterNumericGreater(),
                "FilterNumericGreaterOrEqual" => new FilterNumericGreaterOrEqual(),
                "FilterNumericLess" => new FilterNumericLess(),
                "FilterNumericLessOrEqual" => new FilterNumericLessOrEqual(),

                "FilterStringEquals" => new FilterStringEquals(),
                "FilterStringContains" => new FilterStringContains(),
                "FilterStringBeginsWith" => new FilterStringBeginsWith(),
                "FilterStringEndsWith" => new FilterStringEndsWith(),
                "FilterStringGreater" => new FilterStringGreater(),
                "FilterStringLess" => new FilterStringLess(),

                _ => throw new ArgumentException($"Unknown evaluator: {evaluatorName}")
            };
        }
    }
}
