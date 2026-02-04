#include "../include/code_intelligence.h"
#include <nlohmann/json.hpp>
#include <fstream>
#include <iostream>

using json = nlohmann::json;

namespace CodeIntelligence {

bool CompileCommandsParser::loadFromFile(const std::string& filePath) {
    std::ifstream file(filePath);
    if (!file.is_open()) {
        std::cerr << "Could not open compile commands file: " << filePath << std::endl;
        return false;
    }
    
    try {
        json j;
        file >> j;
        
        commands.clear();
        
        for (const auto& entry : j) {
            CompileCommand command;
            command.directory = entry.value("directory", "");
            command.command = entry.value("command", "");
            command.file = entry.value("file", "");
            
            // Parse arguments from command if available
            if (entry.contains("arguments")) {
                for (const auto& arg : entry["arguments"]) {
                    command.arguments.push_back(arg);
                }
            }
            
            commands.push_back(command);
        }
        
        std::cout << "Loaded " << commands.size() << " compile commands" << std::endl;
        return true;
        
    } catch (const std::exception& e) {
        std::cerr << "Error parsing compile commands: " << e.what() << std::endl;
        return false;
    }
}

} // namespace CodeIntelligence
