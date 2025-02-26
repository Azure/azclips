package core

import (
	"fmt"
	"slices"
	"strings"

	"github.com/Azure/azure-cli-lite/shared/proto"
)

// define the operation for the argument
type argOp interface {
	apply()
}

type ArgActions struct {
	ops []argOp
}

func parseArgs(cmd *proto.CMDInterface, flags []string) ([]ArgActions, []error) {
	argActions := make([]*ArgActions, 0)

	currentFlag := ""
	currentValues := make([]string, 0)
	isGlobal := false
	parseErrs := make([]error, 0)
	for _, flag := range flags {
		if strings.HasPrefix(flag, "-") {
			if currentFlag != flag {
				if currentFlag != "" {
					if isGlobal {
						arg, err := parseGlobalArg(currentFlag, currentValues)
						if err != nil {
							parseErrs = append(parseErrs, err)
						} else {
							argActions = append(argActions, arg)
						}
					} else {
						arg, err := parseCmdArg(cmd, currentFlag, currentValues)
						if err != nil {
							parseErrs = append(parseErrs, err)
						} else {
							argActions = append(argActions, arg)
						}
					}
				}
				currentFlag = flag
				currentValues = make([]string, 0)
				isGlobal = false
				for _, cmdArg := range globalArgs {
					if slices.Contains(cmdArg.Options, flag) {
						isGlobal = true
						break
					}
				}
			}
			currentValues = append(currentValues, flag)
		}
	}

	if len(parseErrs) > 0 {
		return nil, parseErrs
	}

	return argActions, nil
}

func parseGlobalArg(flag string, values []string) (*ArgActions, error) {
	actions := ArgActions{}

	// if arg.arg.ArgType.GetStr() != nil {

	// }

	return &actions, nil
}

func parseCmdArg(cmd *proto.CMDInterface, flag string, values []string) (*ArgActions, error) {
	actions := ArgActions{}

	var schema *proto.CMDArg = nil
	for _, arg := range cmd.GetCommand().Args {
		if slices.Contains(arg.Options, flag) {
			schema = arg
			break
		}
	}

	if schema == nil {
		return nil, fmt.Errorf("argument not found: %s", flag)
	}

	// Handle different argument types based on the oneof field
	switch argType := schema.ArgType.(type) {

	case *proto.CMDArg_Str:
		return parseStringArgActions(cmd, schema, values)
	case *proto.CMDArg_Number:
		return parseNumberArgActions(cmd, schema, values)
	case *proto.CMDArg_Boolean:
		return parseBooleanArgActions(cmd, schema, values)

	case *proto.CMDArg_Cls:
		return parseClsArgActions(cmd, schema, values)
	case *proto.CMDArg_Object:
		return parseObjectArgActions(cmd, schema, values)
	case *proto.CMDArg_Array:
		return parseArrayArgActions(cmd, schema, values)

	case *proto.CMDArg_AnyType:
		return parseAnyTypeArgActions(cmd, schema, values)

	default:
		return nil, fmt.Errorf("unsupported argument type for %s", schema.VarName)
	}
}

func parseClsArgActions(cmd *proto.CMDInterface, schema *proto.CMDArg, values []string) (*ArgActions, error) {

	return &actions, nil
}

func parseSimpleTypeArgActions(cmd *proto.CMDInterface, schema *proto.CMDArg, values []string) (*ArgActions, error) {
	actions := ArgActions{}

	return &actions, nil
}

func parseNumberArgActions(cmd *proto.CMDInterface, schema *proto.CMDArg, values []string) (*ArgActions, error) {
	actions := ArgActions{}

	return &actions, nil
}

func parseBooleanArgActions(cmd *proto.CMDInterface, schema *proto.CMDArg, values []string) (*ArgActions, error) {
	actions := ArgActions{}

	return &actions, nil
}

func parseObjectArgActions(cmd *proto.CMDInterface, schema *proto.CMDArg, values []string) (*ArgActions, error) {
	actions := ArgActions{}

	return &actions, nil
}

func parseArrayArgActions(cmd *proto.CMDInterface, schema *proto.CMDArg, values []string) (*ArgActions, error) {
	actions := ArgActions{}

	return &actions, nil
}

func parseAnyTypeArgActions(cmd *proto.CMDInterface, schema *proto.CMDArg, values []string) (*ArgActions, error) {
	actions := ArgActions{}

	return &actions, nil
}

// retrieve the class argument schema from the command interface
func retrieveClsArgImplInCmd(cmd *proto.CMDInterface, clsName string) *proto.CMDArg {
	for _, arg := range cmd.GetCommand().Args {
		if obj := arg.GetObject(); obj != nil {
			if obj.GetClsType() == clsName {
				return arg
			}
			if arg := retrieveClsArgImplInObj(obj, clsName); arg != nil {
				return arg
			}
		} else if arr := arg.GetArray(); arr != nil {
			if arr.GetClsType() == clsName {
				return arg
			}
			if arg := retrieveClsArgImplInArray(arr, clsName); arg != nil {
				return arg
			}
		}
	}
	return nil
}

func retrieveClsArgImplInObj(obj *proto.CMDObjectArg, clsName string) *proto.CMDArg {
	for _, arg := range obj.Props {
		if obj := arg.GetObject(); obj != nil {
			if obj.GetClsType() == clsName {
				return arg
			}
			if arg := retrieveClsArgImplInObj(obj, clsName); arg == nil {
				return arg
			}
		} else if arr := arg.GetArray(); arr != nil {
			if arr.GetClsType() == clsName {
				return arg
			}
			if arg := retrieveClsArgImplInArray(arr, clsName); arg != nil {
				return arg
			}
		}
	}
	if obj.AdditionalProps != nil {
		arg := obj.AdditionalProps.Item
		if obj := arg.GetObject(); obj != nil {
			if obj.GetClsType() == clsName {
				return arg
			}
			if arg := retrieveClsArgImplInObj(obj, clsName); arg == nil {
				return arg
			}
		} else if arr := arg.GetArray(); arr != nil {
			if arr.GetClsType() == clsName {
				return arg
			}
			if arg := retrieveClsArgImplInArray(arr, clsName); arg != nil {
				return arg
			}
		}
	}
	return nil
}

func retrieveClsArgImplInArray(arr *proto.CMDArrayArg, clsName string) *proto.CMDArg {
	arg := arr.Item
	if obj := arg.GetObject(); obj != nil {
		if obj.GetClsType() == clsName {
			return arg
		}
		if arg := retrieveClsArgImplInObj(obj, clsName); arg == nil {
			return arg
		}
	} else if arr := arg.GetArray(); arr != nil {
		if arr.GetClsType() == clsName {
			return arg
		}
		if arg := retrieveClsArgImplInArray(arr, clsName); arg != nil {
			return arg
		}
	}
	return nil
}

// get the value from the prompt
type argPromptInputOp struct {
	prompt *proto.CMDArgPromptInput
}

func (o *argPromptInputOp) apply() {
}

// get the value
type argValueAssignOp struct {
	keys  []string
	value ArgValue
}

func (o *argValueAssignOp) apply() {
}
