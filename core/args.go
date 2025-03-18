package core

import (
	"github.com/Azure/azure-cli-lite/shared/proto"
)

var globalArgs = []*proto.CMDArg{
	{
		VarName: "Global.help",
		Name:    "help",
		Options: []string{"--help", "-h"},
		Help: &proto.CMDArgHelp{
			Short: "Show this help message and exit.",
		},
		Blank: &proto.CMDArgBlank{
			Value: "prompt",
		},
		ArgType: &proto.CMDArg_Str{
			Str: &proto.CMDStringArg{
				Enum: &proto.CMDArgEnum{
					Items: []*proto.CMDArgEnumItem{
						{
							Name:  "plain",
							Value: "plain",
						},
						{
							Name:  "prompt",
							Value: "prompt",
						},
					},
				},
			},
		},
	},
	{
		VarName: "Global.debug",
		Name:    "debug",
		Options: []string{"--debug"},
		Help: &proto.CMDArgHelp{
			Short: "show all debug logs.",
		},
	},
	{
		VarName: "Global.output",
		Name:    "output",
		Options: []string{"--output", "-o"},
		Help: &proto.CMDArgHelp{
			Short: "Output format.",
		},
		Default: &proto.CMDArgDefault{
			Value: "json",
		},
		ArgType: &proto.CMDArg_Str{
			Str: &proto.CMDStringArg{
				Enum: &proto.CMDArgEnum{
					Items: []*proto.CMDArgEnumItem{
						{
							Name:  "json",
							Value: "json",
						},
						{
							Name:  "jsonc",
							Value: "jsonc",
						},
						{
							Name:  "none",
							Value: "none",
						},
						{
							Name:  "table",
							Value: "table",
						},
						{
							Name:  "tsv",
							Value: "tsv",
						},
						{
							Name:  "yaml",
							Value: "yaml",
						},
						{
							Name:  "yamlc",
							Value: "yamlc",
						},
					},
				},
			},
		},
	},
	{
		VarName: "Global.query",
		Name:    "query",
		Options: []string{"--query"},
		Help: &proto.CMDArgHelp{
			Short: "JMESPath query string.",
			Long:  "See http://jmespath.org/ for more information and examples.",
		},
		ArgType: &proto.CMDArg_Str{
			Str: &proto.CMDStringArg{},
		},
	},
}

// // 定义具体的值类型
// type Value struct {
// 	Type string      `json:"type"`
// 	Data interface{} `json:"data"`
// }

// // 实现 ArgValue 接口
// func (v *Value) ToSerializeData() string {
// 	jsonData, _ := json.Marshal(v)
// 	return string(jsonData)
// }
