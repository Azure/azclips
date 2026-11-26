import "./generic-resource-scope.tsp";

using TypeSpec.Http;
using TypeSpec.Rest;
using Azure.ResourceManager;

namespace Test.ScopeRepro;

model SubscriptionScope {
  ...ApiVersionParameter;
  ...SubscriptionIdParameter;
  ...Azure.ResourceManager.Legacy.Provider;
}

model ResourceGroupScope {
  ...ApiVersionParameter;
  ...SubscriptionIdParameter;
  ...ResourceGroupParameter;
  ...Azure.ResourceManager.Legacy.Provider;
}

alias ScopedWidgetOps<Scope extends {}, Collection extends valueof string> =
  Azure.ResourceManager.Legacy.LegacyOperations<
    Scope,
    {
      @path
      @key
      @segment(Collection)
      widgetName: string;
    }
  >;

alias SubscriptionWidgetOps = ScopedWidgetOps<SubscriptionScope, "genericWidgets">;
alias ReadOnlyWidgetOps = ScopedWidgetOps<SubscriptionScope, "readOnlyWidgets">;

model BackupResult {
  status: string;
}

@error
model WidgetError {
  @statusCode statusCode: 404;
  @body body: GenericWidget;
}

@armResourceOperations(#{ allowStaticRoutes: true })
interface MoreGenericWidgets {
  getAtSubscription is SubscriptionWidgetOps.Read<GenericWidget>;
  getReadOnly is ReadOnlyWidgetOps.Read<GenericWidget>;

  @get
  @route("/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Test.ScopeRepro/genericWidgets/{genericWidgetName}/backups/{backupName}")
  @armResourceRead(GenericWidget)
  getBackup(
    ...ApiVersionParameter,
    ...SubscriptionIdParameter,
    ...ResourceGroupParameter,
    @path genericWidgetName: string,
    @path backupName: string,
  ): ArmResponse<BackupResult> | WidgetError;

  @get
  @route("/subscriptions/{subscriptionId}/providers/Other.Provider/foreignWidgets/{genericWidgetName}")
  @armResourceRead(GenericWidget)
  getForeign(
    ...ApiVersionParameter,
    ...SubscriptionIdParameter,
    @path genericWidgetName: string,
  ): ArmResponse<GenericWidget>;

  @put
  @route("/unrecognized/providers/Test.ScopeRepro/unknownWidgets/{genericWidgetName}")
  @armResourceCreateOrUpdate(GenericWidget)
  createAtUnknownScope(
    ...ApiVersionParameter,
    @path genericWidgetName: string,
    @body body: GenericWidget,
  ): ArmResponse<GenericWidget>;
}

model SeparateWidget is Azure.ResourceManager.Legacy.GenericResource<WidgetProperties> {}

model CreateSeparateWidget {
  properties: WidgetProperties;
  requiredValue: string;
}

alias SeparateWidgetOps = ScopedWidgetOps<ResourceGroupScope, "separateWidgets">;

@armResourceOperations
interface SeparateWidgets {
  get is SeparateWidgetOps.Read<SeparateWidget>;
  createOrUpdate is SeparateWidgetOps.CreateOrUpdateAsync<
    SeparateWidget,
    Request = CreateSeparateWidget
  >;
}

model AmbiguousWidget is Azure.ResourceManager.Legacy.GenericResource<WidgetProperties> {}

model OtherCreateWidget {
  otherValue: string;
}

alias AmbiguousGroupOps = ScopedWidgetOps<ResourceGroupScope, "ambiguousWidgets">;
alias AmbiguousSubscriptionOps = ScopedWidgetOps<SubscriptionScope, "ambiguousWidgets">;

@armResourceOperations
interface AmbiguousWidgets {
  createAtGroup is AmbiguousGroupOps.CreateOrUpdateAsync<
    AmbiguousWidget,
    Request = CreateSeparateWidget
  >;
  createAtSubscription is AmbiguousSubscriptionOps.CreateOrUpdateAsync<
    AmbiguousWidget,
    Request = OtherCreateWidget
  >;
}
