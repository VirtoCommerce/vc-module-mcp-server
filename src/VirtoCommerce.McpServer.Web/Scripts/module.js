// Call this to register your module to main application
var moduleName = 'McpServer';

if (AppDependencies !== undefined) {
    AppDependencies.push(moduleName);
}

angular.module(moduleName, [])
    .config(['$stateProvider',
        function ($stateProvider) {
            $stateProvider
                .state('workspace.McpServerState', {
                    url: '/McpServer',
                    templateUrl: '$(Platform)/Scripts/common/templates/home.tpl.html',
                    controller: [
                        'platformWebApp.bladeNavigationService',
                        function (bladeNavigationService) {
                            var newBlade = {
                                id: 'blade1',
                                controller: 'McpServer.helloWorldController',
                                template: 'Modules/$(VirtoCommerce.McpServer)/Scripts/blades/hello-world.html',
                                isClosingDisabled: true,
                            };
                            bladeNavigationService.showBlade(newBlade);
                        }
                    ]
                });
        }
    ])
    .run(['platformWebApp.mainMenuService', '$state',
        function (mainMenuService, $state) {
            //Register module in main menu
            var menuItem = {
                path: 'browse/McpServer',
                icon: 'fa fa-cube',
                title: 'McpServer',
                priority: 100,
                action: function () { $state.go('workspace.McpServerState'); },
                permission: 'McpServer:access',
            };
            mainMenuService.addMenuItem(menuItem);
        }
    ]);
