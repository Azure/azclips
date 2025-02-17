package core

import (
	"github.com/Azure/azure-cli-lite/shared/proto"
)

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
